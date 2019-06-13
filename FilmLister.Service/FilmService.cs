﻿using FilmLister.Domain;
using FilmLister.Persistence;
using FilmLister.Service.Exceptions;
using FilmLister.TmdbIntegration;
using FilmLister.TmdbIntegration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmLister.Service
{
    public class FilmService
    {
        private readonly OrderService orderService;

        private readonly TmdbService tmdbService;

        private readonly FilmListerContext filmListerContext;

        public FilmService(OrderService orderService, TmdbService tmdbService, FilmListerContext filmListerContext)
        {
            this.orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            this.tmdbService = tmdbService ?? throw new ArgumentNullException(nameof(tmdbService));
            this.filmListerContext = filmListerContext ?? throw new ArgumentNullException(nameof(filmListerContext));
        }

        public async Task<Domain.Film[]> RetrieveFilms()
        {
            var films = await filmListerContext.Films.ToArrayAsync();

            var domainFilms = films.Select(f => Map(f)).ToArray();
            return domainFilms;
        }

        public async Task<Domain.FilmListTemplate[]> RetrieveFilmListTemplates()
        {
            var filmListTemplates = await filmListerContext.FilmListTemplates
                .Include(l => l.FilmListItems)
                    .ThenInclude(i => i.Film)
                .ToArrayAsync();

            var domainLists = filmListTemplates.Select(l => Map(l)).ToArray();
            return domainLists;
        }

        /// <summary>
        /// Creates a new list for ordering. Returns ID of the list.
        /// </summary>
        /// <param name="filmListTemplateId"></param>
        /// <returns></returns>
        public async Task<int> CreateOrderedFilmList(int filmListTemplateId)
        {
            var filmListTemplate = await RetrieveFilmListTemplateById(filmListTemplateId);
            var persistenceListTemplate = await filmListerContext.FilmListTemplates
                .Include(l => l.FilmListItems)
                    .ThenInclude(i => i.Film)
                .FirstAsync(l => l.Id == filmListTemplateId);

            var films = persistenceListTemplate.FilmListItems.Select(i => new Persistence.OrderedFilm() { Film = i.Film }).ToList();
            var filmList = new Persistence.OrderedList()
            {
                OrderedFilms = films,
                FilmListTemplate = persistenceListTemplate
            };

            await filmListerContext.OrderedLists.AddAsync(filmList);
            await filmListerContext.SaveChangesAsync();

            return filmList.Id;
        }

        public async Task SubmitFilmChoice(int id, int lesserFilmId, int greaterFilmId)
        {
            var orderedFilmList = await filmListerContext.OrderedLists
                .Include(ol => ol.OrderedFilms)
                    .ThenInclude(f => f.Film)
                .FirstOrDefaultAsync(ol => ol.Id == id);
            if (orderedFilmList != null)
            {
                var lesser = orderedFilmList.OrderedFilms.FirstOrDefault(f => f.Id == lesserFilmId);
                var greater = orderedFilmList.OrderedFilms.FirstOrDefault(f => f.Id == greaterFilmId);

                if (lesser == null)
                {
                    throw new FilmNotFoundException(lesserFilmId);
                }
                else if (greater == null)
                {
                    throw new FilmNotFoundException(greaterFilmId);
                }
                else
                {
                    if (lesser.GreaterRankedFilmItems == null)
                    {
                        lesser.GreaterRankedFilmItems = new List<Persistence.OrderedFilmRankItem>();
                    }
                    var rankItem = new OrderedFilmRankItem
                    {
                        LesserRankedFilm = lesser,
                        GreaterRankedFilm = greater
                    };
                    await filmListerContext.OrderedFilmRankItems.AddAsync(rankItem);
                    await filmListerContext.SaveChangesAsync();
                }
            }
            else
            {
                throw new ListNotFoundException(id);
            }
        }

        public async Task SubmitIgnoreFilm(int id, int filmId)
        {
            var orderedFilmList = await filmListerContext.OrderedLists
                .Include(ol => ol.OrderedFilms)
                    .ThenInclude(f => f.Film)
                .FirstOrDefaultAsync(ol => ol.Id == id);
            if (orderedFilmList != null)
            {
                var filmToIgnore = orderedFilmList.OrderedFilms.FirstOrDefault(f => f.Id == filmId);

                if (filmToIgnore == null)
                {
                    throw new FilmNotFoundException(filmId);
                }
                else
                {
                    filmToIgnore.Ignored = true;
                    await filmListerContext.SaveChangesAsync();
                }
            }
            else
            {
                throw new ListNotFoundException(id);
            }
        }

        /// <summary>
        /// Attempts to order a list. Further information might be required, which is requested in the return object.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<OrderedFilmList> AttemptListOrder(int id)
        {
            var persistenceOrderedFilmList = await filmListerContext.OrderedLists
                .Include(ol => ol.OrderedFilms)
                    .ThenInclude(f => f.GreaterRankedFilmItems)
                .Include(ol => ol.OrderedFilms)
                    .ThenInclude(f => f.Film)
                .FirstOrDefaultAsync(ol => ol.Id == id);

            if (persistenceOrderedFilmList == null)
            {
                throw new ListNotFoundException(id);
            }

            var orderedFilmList = Map(persistenceOrderedFilmList);
            var orderResult = orderService.OrderFilms(orderedFilmList.SortedFilms.Where(f => !f.Ignore));

            var filmList = new Domain.OrderedFilmList(
                orderedFilmList.Id,
                orderResult.Completed,
                orderResult.SortedResults.ToArray(),
                persistenceOrderedFilmList.OrderedFilms.Where(f => f.Ignored).Select(f => Map(f)).ToArray(),
                orderResult.LeftSort,
                orderResult.RightSort);

            if (persistenceOrderedFilmList.Completed != filmList.Completed)
            {
                persistenceOrderedFilmList.Completed = filmList.Completed;
                await filmListerContext.SaveChangesAsync();
            }

            return filmList;
        }

        public async Task<Domain.Film> RetrieveFilmByTmdbId(int tmdbId)
        {
            var film = await filmListerContext.Films.FirstOrDefaultAsync(f => f.TmdbId == tmdbId);
            if (film == null)
            {
                // try tmdb
                var movieDetail = await tmdbService.FetchMovieDetails(tmdbId);
                film = new Persistence.Film
                {
                    Name = movieDetail.Title,
                    ImageUrl = CreateFullImagePath(movieDetail.PosterPath),
                    ImdbId = movieDetail.ImdbId,
                    TmdbId = tmdbId,
                    ReleaseDate = movieDetail.ReleaseDate
                };

                await filmListerContext.Films.AddAsync(film);
                await filmListerContext.SaveChangesAsync();
            }

            var domainFilm = Map(film);
            return domainFilm;
        }

        public async Task<Domain.FilmListTemplate> RetrieveFilmListTemplateById(int id)
        {
            var list = await filmListerContext.FilmListTemplates
                .Include(l => l.FilmListItems)
                    .ThenInclude(i => i.Film)
                .FirstOrDefaultAsync(l => l.Id == id);
            var domainList = Map(list);
            return domainList;
        }

        public async Task DeleteFilmListTemplateById(int id)
        {
            var list = await filmListerContext.FilmListTemplates
                .Include(l => l.FilmListItems)
                    .ThenInclude(i => i.Film)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list != null)
            {
                var derivedLists = await filmListerContext
                    .OrderedLists
                    .Include(l => l.OrderedFilms)
                        .ThenInclude(f => f.GreaterRankedFilmItems)
                    .Include(l => l.OrderedFilms)
                        .ThenInclude(f => f.LesserRankedFilmItems)
                    .Where(l => l.FilmListTemplate == list)
                    .ToArrayAsync();

                var derivedFilms = derivedLists
                    .Where(l => l.OrderedFilms != null)
                    .SelectMany(l => l.OrderedFilms)
                    .ToArray();

                var derivedRankings = derivedFilms
                    .SelectMany(f => f.GreaterRankedFilmItems)
                    .Union(derivedFilms
                    .SelectMany(f => f.LesserRankedFilmItems))
                    .ToArray();

                filmListerContext.OrderedFilmRankItems.RemoveRange(derivedRankings);
                filmListerContext.OrderedFilms.RemoveRange(derivedFilms);
                filmListerContext.OrderedLists.RemoveRange(derivedLists);
                filmListerContext.FilmListItems.RemoveRange(list.FilmListItems);
                filmListerContext.FilmListTemplates.Remove(list);

                await filmListerContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Adds a film to the film list template.
        /// </summary>
        /// <param name="filmListTemplate"></param>
        /// <param name="film"></param>
        /// <exception cref="FilmAlreadyInFilmListTemplateException">Occurs when the given film is already in the list.</exception>
        /// <returns></returns>
        public async Task<Domain.FilmListTemplate> AddFilmToFilmListTemplate(Domain.FilmListTemplate filmListTemplate, Domain.Film film)
        {
            var persistenceList = await filmListerContext.FilmListTemplates.FirstAsync(l => l.Id == filmListTemplate.Id);

            if (persistenceList.FilmListItems == null)
            {
                persistenceList.FilmListItems = new List<FilmListItem>();
            }
            if (persistenceList.FilmListItems.Any(item => item.Film.Id == film.Id))
            {
                throw new FilmAlreadyInFilmListTemplateException(film.Id, filmListTemplate.Id);
            }

            var persistenceFilm = await filmListerContext.Films.FirstAsync(l => l.Id == film.Id);
            persistenceList.FilmListItems.Add(new FilmListItem
            {
                Film = persistenceFilm,
                FilmListTemplate = persistenceList
            });
            await filmListerContext.SaveChangesAsync();

            var list = await RetrieveFilmListTemplateById(persistenceList.Id);
            return list;
        }

        /// <summary>
        /// Creates a full image path from part of a path. If image path is null
        /// then an empty string is returned.
        /// </summary>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        public string CreateFullImagePath(string imagePath)
        {
            string fullPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                fullPath = $"https://image.tmdb.org/t/p/w500/{imagePath}";
            }
            return fullPath;
        }

        public async Task<FilmTitle[]> SearchFilmTitles(string query)
        {
            var searchResult = await tmdbService.SearchMovies(query);
            var titles = searchResult.Results
                .Select(r => new FilmTitle(r.Id, r.Title, CreateFullImagePath(r.PosterPath), r.ReleaseDate?.Year))
                .ToArray();
            return titles;
        }

        public async Task<Domain.Person[]> SearchPersons(string query)
        {
            var peopleResults = await tmdbService.SearchPeople(query);
            var result = peopleResults.Results.Select(r => Map(r)).ToArray();
            return result;
        }

        public async Task<FilmTitleWithPersonCredit[]> FetchFilmTitlesByPersonId(int tmdbId)
        {
            var titles = new List<FilmTitleWithPersonCredit>();
            var credit = await tmdbService.FetchPersonMovieCredits(tmdbId);
            foreach (var r in credit.Persons)
            {
                titles.Add(new FilmTitleWithPersonCredit(
                    r.Id,
                    r.Title,
                    CreateFullImagePath(r.PosterPath),
                    r.ReleaseDate?.Year,
                    tmdbId,
                    r.Job));
            }

            return titles.ToArray();
        }

        private Domain.Person Map(TmdbIntegration.Models.Person person)
        {
            Domain.Person personDomain = null;
            if (person != null)
            {
                personDomain = new Domain.Person(person.Id, person.Title);
            }
            return personDomain;
        }


        private Domain.Person Map(PersonSearchResult personSearchResult)
        {
            Domain.Person personDomain = null;
            if (personSearchResult != null)
            {
                personDomain = new Domain.Person(personSearchResult.Id, personSearchResult.Name);
            }
            return personDomain;
        }

        private Domain.Film Map(Persistence.Film film)
        {
            Domain.Film filmModel = null;
            if (film != null)
            {
                filmModel = new Domain.Film(film.Id, film.Name, film.TmdbId, film.ImageUrl, film.ImdbId, film.ReleaseDate?.Year);
            }
            return filmModel;
        }

        private Domain.OrderedFilm Map(Persistence.OrderedFilm film)
        {
            Domain.OrderedFilm filmModel = null;
            if (film != null)
            {
                filmModel = new Domain.OrderedFilm(film.Id, film.Ignored, Map(film.Film));
            }
            return filmModel;
        }

        private Domain.FilmListTemplate Map(Persistence.FilmListTemplate orderedFilmList)
        {
            List<Domain.Film> films = new List<Domain.Film>();
            if (orderedFilmList?.FilmListItems != null)
            {
                films.AddRange(orderedFilmList.FilmListItems.Select(f => Map(f.Film)));
            }
            var filmList = new Domain.FilmListTemplate(
                orderedFilmList.Id,
                orderedFilmList.Name,
                films.ToArray());
            return filmList;
        }

        private Domain.OrderedFilmList Map(Persistence.OrderedList orderedFilmList)
        {
            var films = new List<Domain.OrderedFilm>();
            if (orderedFilmList?.OrderedFilms != null)
            {
                var mapping = orderedFilmList.OrderedFilms.ToDictionary(k => k, v => Map(v));
                foreach (var kv in mapping.Where(m => m.Key.LesserRankedFilmItems != null))
                {
                    foreach (var greater in kv.Key.LesserRankedFilmItems)
                    {
                        var domain = mapping[greater.GreaterRankedFilm];
                        kv.Value.AddHigherRankedObject(domain);
                    }
                }
                films.AddRange(mapping.Values);
            }
            var filmList = new Domain.OrderedFilmList(
                orderedFilmList.Id,
                orderedFilmList.Completed,
                films,
                new Domain.OrderedFilm[0],
                null,
                null);
            return filmList;
        }
    }
}
