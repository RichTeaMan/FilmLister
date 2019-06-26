﻿using FilmLister.Persistence;
using FilmLister.Service;
using FilmLister.Service.Exceptions;
using FilmLister.WebUI.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FilmLister.WebUI.Controllers
{
    public class FilmListTemplateController : Controller
    {
        private readonly FilmListerContext filmListerContext;

        private readonly FilmListTemplateMapper filmListTemplateMapper;

        private readonly FilmService filmService;

        public FilmListTemplateController(FilmListerContext filmListerContext, FilmListTemplateMapper filmListTemplateMapper, FilmService filmService)
        {
            this.filmListerContext = filmListerContext ?? throw new ArgumentNullException(nameof(filmListerContext));
            this.filmListTemplateMapper = filmListTemplateMapper ?? throw new ArgumentNullException(nameof(filmListTemplateMapper));
            this.filmService = filmService ?? throw new ArgumentNullException(nameof(filmService));
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var filmListTemplate = await filmService.CreateFilmListTemplate();
            return RedirectToAction("View", new { filmListTemplateId = filmListTemplate.Id });
        }

        public async Task<IActionResult> View(int filmListTemplateId)
        {
            IActionResult result;
            var list = await filmService.RetrieveFilmListTemplateById(filmListTemplateId);
            if (list != null)
            {
                var listModel = filmListTemplateMapper.Map(list);
                result = View(listModel);
            }
            else
            {
                result = NotFound("Film list template with given ID not found.");
            }
            return result;
        }

        [HttpPost]
        public async Task<IActionResult> AddFilm(int filmListTemplateId, int tmdbId)
        {
            IActionResult result;
            var list = await filmService.RetrieveFilmListTemplateById(filmListTemplateId);
            var film = await filmService.RetrieveFilmByTmdbId(tmdbId);

            if (list != null && film != null)
            {
                var updatedList = await filmService.AddFilmToFilmListTemplate(list, film);
                var listModel = filmListTemplateMapper.Map(updatedList);
                result = RedirectToAction("View", new { filmListTemplateId = list.Id });
            }
            else
            {
                result = NotFound("Film list template with given ID not found.");
            }
            return result;
        }

        [HttpPost]
        public async Task<IActionResult> Rename(Models.FilmListTemplate filmListTemplate)
        {
            if (!ModelState.IsValid)
            {
                return View(filmListTemplate);
            }
            try
            {
                await filmService.RenameFilmListTemplate(filmListTemplate.Id, filmListTemplate.Name);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ListNotFoundException)
            {
                Console.WriteLine($"Error occurred: {ex.Message}.");
            }

            return RedirectToAction("View", new { filmListTemplateId = filmListTemplate.Id });
        }

        public async Task<IActionResult> List()
        {
            var templates = await filmService.RetrieveFilmListTemplates();
            var modelTemplates = templates.Select(l => filmListTemplateMapper.Map(l)).ToArray();
            return View(modelTemplates);
        }

        [Authorize]
        public async Task<IActionResult> ConfirmDelete(int filmListTemplateId)
        {
            IActionResult result;
            var list = await filmService.RetrieveFilmListTemplateById(filmListTemplateId);

            if (list != null)
            {
                var listModel = filmListTemplateMapper.Map(list);
                return View(listModel);
            }
            else
            {
                result = NotFound("Film list template with given ID not found.");
            }
            return result;
        }

        [Authorize]
        public async Task<IActionResult> Delete(int filmListTemplateId)
        {
            await filmService.DeleteFilmListTemplateById(filmListTemplateId);
            return RedirectToAction(nameof(List));
        }
    }
}
