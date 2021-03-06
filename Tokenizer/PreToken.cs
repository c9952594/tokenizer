﻿using System.Collections.Generic;
using System.Text;

namespace Tokens
{
    /// <summary>
    /// Intermediate data structure that holds the syntactically verified
    /// template token data.
    /// </summary>
    internal class PreToken
    {
        private readonly StringBuilder preamble;
        private readonly StringBuilder name;

        public PreToken()
        {
            Decorators = new List<PreTokenDecorator>();
            preamble = new StringBuilder();
            name = new StringBuilder();
        }

        public int Id { get; set; }

        public int DependsOnId { get; set; }

        public string Preamble => preamble.ToString();

        public string Name => name.ToString();

        public bool Optional { get; set; }

        public bool TerminateOnNewline { get; set; }

        public bool Repeating { get; set; }

        public bool Required { get; set; }

        public IList<PreTokenDecorator> Decorators { get; }

        public void AppendPreamble(string value)
        {
            if (value == "\r") return;

            preamble.Append(value);
        }

        public void AppendName(string value)
        {
            name.Append(value);
        }

        public void AppendDecorators(IEnumerable<PreTokenDecorator> decorators)
        {
            if (decorators == null) return;

            foreach (var decorator in decorators)
            {
                Decorators.Add(decorator);
            }
        }
    }
}
