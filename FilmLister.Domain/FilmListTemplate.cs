﻿using System;

namespace FilmLister.Domain
{
    public class FilmListTemplate
    {
        public int Id { get; }
        public string Name { get; }
        public Film[] Films { get; }

        public FilmListTemplate(int id, string name, Film[] films)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Films = films ?? throw new ArgumentNullException(nameof(films));
        }
    }
}
