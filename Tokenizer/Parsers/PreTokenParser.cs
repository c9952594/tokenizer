﻿using System;
using System.Linq;
using System.Text;
using Tokens.Enumerators;
using Tokens.Exceptions;
using Tokens.Extensions;

namespace Tokens.Parsers
{
    internal class PreTokenParser
    {
        private const string ValidTokenNameCharacters = @"abcdefghijklmnopqrstuvwxyzABCDDEFGHIJKLMNOPQRSTUVWXYZ1234567890_.";

        public PreTemplate Parse(string pattern)
        {
            return Parse(pattern, TokenizerOptions.Defaults);
        }

        public PreTemplate Parse(string pattern, TokenizerOptions options)
        {
            var template = new PreTemplate();
            template.Options = options.Clone();

            var enumerator = new PreTokenEnumerator(pattern);

            if (enumerator.IsEmpty)
            {
                return template;
            }

            var state = FlatTokenParserState.AtStart;
            var token = new PreToken();
            var decorator = new PreTokenDecorator();
            var argument = string.Empty;
            var frontMatterName = new StringBuilder();
            var frontMatterValue = new StringBuilder();

            while (enumerator.IsEmpty == false)
            {
                switch (state)
                {
                    case FlatTokenParserState.AtStart:
                        ParseStart(enumerator, ref state);
                        break;

                    case FlatTokenParserState.InFrontMatter:
                        ParseFrontMatter(enumerator, ref frontMatterName, ref state);
                        break;

                    case FlatTokenParserState.InFrontMatterComment:
                        ParseFrontMatterComment(enumerator, ref state);
                        break;

                    case FlatTokenParserState.InFrontMatterOption:
                        ParseFrontMatterOption(enumerator, ref frontMatterName, ref state);
                        break;

                    case FlatTokenParserState.InFrontMatterOptionValue:
                        ParseFrontMatterOptionValue(template, enumerator, ref frontMatterName, ref frontMatterValue, ref state);
                        break;

                    case FlatTokenParserState.InPreamble:
                        ParsePreamble(ref token, enumerator, ref state);
                        break;

                    case FlatTokenParserState.InTokenName:
                        ParseTokenName(template, ref token, enumerator, ref state);
                        break;

                    case FlatTokenParserState.InDecorator:
                        ParseDecorator(template, ref token, enumerator, ref state, ref decorator);
                        break;

                    case FlatTokenParserState.InDecoratorArgument:
                        ParseDecoratorArgument(enumerator, ref state, ref decorator, ref argument);
                        break;

                    case FlatTokenParserState.InDecoratorArgumentSingleQuotes:
                        ParseDecoratorArgumentInSingleQuotes(enumerator, ref state, ref decorator, ref argument);
                        break;

                    case FlatTokenParserState.InDecoratorArgumentDoubleQuotes:
                        ParseDecoratorArgumentInDoubleQuotes(enumerator, ref state, ref decorator, ref argument);
                        break;

                    case FlatTokenParserState.InDecoratorArgumentRunOff:
                        ParseDecoratorArgumentRunOff(enumerator, ref state);
                        break;


                    default:
                        throw new TokenizerException($"Unknown FlatTokenParserState: {state}");
                }
            }

            // Append current token if it has contents
            // Note: allow empty token values, as these will serve to truncate the last 
            // token in the template
            if (string.IsNullOrWhiteSpace(token.Preamble) == false)
            {
                AppendToken(template, token);
            }

            return template;
        }

        private void ParseStart(PreTokenEnumerator enumerator, ref FlatTokenParserState state)
        {
            var peek = enumerator.Peek(4);

            if (peek == "---\n")
            {
                state = FlatTokenParserState.InFrontMatter;
                enumerator.Next(4);
                return;
            }

            peek = enumerator.Peek(5);

            if (peek == "---\r\n")
            {
                state = FlatTokenParserState.InFrontMatter;
                enumerator.Next(4); // Next() will trim /r/n
                return;
            }

            state = FlatTokenParserState.InPreamble;
        }

        private void ParseFrontMatter(PreTokenEnumerator enumerator, ref StringBuilder frontMatterName, ref FlatTokenParserState state)
        {
            var peek = enumerator.Peek(4);

            if (peek == "---\n")
            {
                state = FlatTokenParserState.InPreamble;
                enumerator.Next(4);
                return;
            }

            peek = enumerator.Peek(5);

            if (peek == "---\r\n")
            {
                state = FlatTokenParserState.InPreamble;
                enumerator.Next(5);
                return;
            }

            var next = enumerator.Next();

            switch (next)
            {
                case "#":
                    state = FlatTokenParserState.InFrontMatterComment;
                    break;

                case "\n":
                    break;

                default:
                    state = FlatTokenParserState.InFrontMatterOption;
                    frontMatterName.Append(next);
                    break;
            }
        }

        private void ParseFrontMatterOption(PreTokenEnumerator enumerator, ref StringBuilder frontMatterName, ref FlatTokenParserState state)
        {
            var next = enumerator.Next();

            switch (next)
            {
                case ":":
                    state = FlatTokenParserState.InFrontMatterOptionValue;
                    break;

                default:
                    frontMatterName.Append(next);
                    break;
            }
        }

        private void ParseFrontMatterOptionValue(PreTemplate template, PreTokenEnumerator enumerator, ref StringBuilder frontMatterName, ref StringBuilder frontMatterValue, ref FlatTokenParserState state)
        {
            var next = enumerator.Next();

            switch (next)
            {
                case "\n":
                    var rawName = frontMatterName.ToString().Trim();
                    var name = frontMatterName.ToString().Trim().ToLowerInvariant();
                    var value = frontMatterValue.ToString().Trim().ToLowerInvariant();

                    switch (name)
                    {
                        case "trimleadingwhitespace":
                            var trimLeadingWhitespaceInTokenPreamble = ConvertFrontMatterOptionToBool(value, rawName, enumerator);
                            template.Options.TrimLeadingWhitespaceInTokenPreamble = trimLeadingWhitespaceInTokenPreamble;
                            break;
                        case "trimtrailingwhitespace":
                            var trimTrailingWhiteSpace = ConvertFrontMatterOptionToBool(value, rawName, enumerator);
                            template.Options.TrimTrailingWhiteSpace = trimTrailingWhiteSpace;
                            break;
                        case "outoforder":
                            var outOfOrderTokens = ConvertFrontMatterOptionToBool(value, rawName, enumerator);
                            template.Options.OutOfOrderTokens = outOfOrderTokens;
                            break;
                        case "name":
                            template.Name = frontMatterValue.ToString().Trim();
                            break;
                        case "hint":
                            template.Hints.Add(new Hint
                            {
                                Text = frontMatterValue.ToString().Trim(),
                                Optional = false
                            }); 
                            break;
                        case "hint?":
                            template.Hints.Add(new Hint
                            {
                                Text = frontMatterValue.ToString().Trim(),
                                Optional = true
                            }); 
                            break;
                        case "casesensitive":
                            var caseSensitive = ConvertFrontMatterOptionToBool(value, rawName, enumerator);
                            if (caseSensitive)
                            {
                                template.Options.TokenStringComparison = StringComparison.InvariantCulture;
                            }
                            else
                            {
                                template.Options.TokenStringComparison = StringComparison.InvariantCultureIgnoreCase;
                            }
                            break;

                        default:
                            throw new ParsingException($"Unknown front matter option: {rawName}", enumerator);
                    }

                    frontMatterName.Clear();
                    frontMatterValue.Clear();
                    state = FlatTokenParserState.InFrontMatter;
                    break;

                default:
                    frontMatterValue.Append(next);
                    break;
            }
        }

        private bool ConvertFrontMatterOptionToBool(string input, string rawName, PreTokenEnumerator enumerator)
        {
            if (bool.TryParse(input, out var asBool))
            {
                return asBool;
            }

            throw new ParsingException($"Unable to convert front matter option to boolean: {rawName}", enumerator);
        }

        private void ParseFrontMatterComment(PreTokenEnumerator enumerator, ref FlatTokenParserState state)
        {
            var next = enumerator.Next();

            switch (next)
            {
                case "\n":
                    state = FlatTokenParserState.InFrontMatter;
                    break;
            }
        }

        private void ParsePreamble(ref PreToken token, PreTokenEnumerator enumerator, ref FlatTokenParserState state)
        {
            var next = enumerator.Next();

            switch (next)
            {
                case "{":
                    if (enumerator.Peek() == "{")
                    {
                        token.AppendPreamble("{");
                        enumerator.Next();
                    }
                    else
                    {
                        state = FlatTokenParserState.InTokenName;
                    }
                    break;

                case "}":
                    if (enumerator.Peek() == "}")
                    {
                        token.AppendPreamble("}");
                        enumerator.Next();
                        break;
                    }
                    throw new ParsingException($"Unescaped character '}}' in template.", enumerator); 


                default:
                    token.AppendPreamble(next);
                    break;
            }
        }

        private void ParseTokenName(PreTemplate template, ref PreToken token, PreTokenEnumerator enumerator, ref FlatTokenParserState state)
        {
            var next = enumerator.Next();
            var peek = enumerator.Peek();

            switch (next)
            {
                case "{":
                    throw new ParsingException($"Unexpected character '{{' in token '{token.Name}'", enumerator); 

                case "}":
                    AppendToken(template, token);
                    token = new PreToken();
                    state = FlatTokenParserState.InPreamble;
                    break;

                case "$":
                    token.TerminateOnNewline = true;
                    switch (peek)
                    {
                        case " ":
                        case "?":
                        case "*":
                        case "}":
                        case ":":
                        case "!":
                            break;

                        default:
                            throw new ParsingException($"Invalid character '{peek}' in token '{token.Name}'", enumerator);
                    }
                    break;

                case "?":
                    token.Optional = true;
                    switch (peek)
                    {
                        case " ":
                        case "$":
                        case "*":
                        case "}":
                        case ":":
                        case "!":
                            break;

                        default:
                            throw new ParsingException($"Invalid character '{peek}' in token '{token.Name}'", enumerator);
                    }

                    if (token.Required) throw new ParsingException($"Required token {token.Name} can't be Optional", enumerator);

                    break;

                case "*":
                    token.Repeating = true;
                    token.Optional = true;
                    switch (peek)
                    {
                        case " ":
                        case "$":
                        case "?":
                        case "}":
                        case ":":
                        case "!":
                            break;

                        default:
                            throw new ParsingException($"Invalid character '{peek}' in token '{token.Name}'", enumerator);
                    }
                    break;

                case "!":
                    token.Required = true;
                    switch (peek)
                    {
                        case " ":
                        case "*":
                        case "$":
                        case "?":
                        case "}":
                        case ":":
                            break;

                        default:
                            throw new ParsingException($"Invalid character '{peek}' in token '{token.Name}'", enumerator);
                    }

                    if (token.Optional) throw new ParsingException($"Optional token {token.Name} can't be Required", enumerator);

                    break;

                case ":":
                    state = FlatTokenParserState.InDecorator;
                    break;

                case " ":
                    switch (peek)
                    {
                        case " ":
                        case "*":
                        case "$":
                        case "?":
                        case "}":
                        case ":":
                        case "!":
                            break;

                        default:
                            if (string.IsNullOrWhiteSpace(token.Name) == false)
                            {
                                throw new ParsingException($"Invalid character '{peek}' in token '{token.Name}'", enumerator);
                            }
                            break;
                    }

                    break;

                default:
                    if (ValidTokenNameCharacters.Contains(next))
                    {
                        token.AppendName(next);
                    }
                    else
                    {
                        throw new ParsingException($"Invalid character '{next}' in token '{token.Name}'", enumerator);
                    }
                    break;
            }
        }

        private void ParseDecorator(PreTemplate template, ref PreToken token, PreTokenEnumerator enumerator, ref FlatTokenParserState state, ref PreTokenDecorator decorator)
        {
            var next = enumerator.Next();

            if (string.IsNullOrWhiteSpace(next)) return;

            switch (next)
            {
                case "}":
                    AppendDecorator(enumerator, token, decorator);
                    AppendToken(template, token);
                    token = new PreToken();
                    decorator = new PreTokenDecorator();
                    state = FlatTokenParserState.InPreamble;
                    break;

                case ",":
                    AppendDecorator(enumerator, token, decorator);
                    decorator = new PreTokenDecorator();
                    break;

                case "(":
                    state = FlatTokenParserState.InDecoratorArgument;
                    break;

                default:
                    decorator.AppendName(next);
                    break;
            }

        }

        private void ParseDecoratorArgument(PreTokenEnumerator enumerator, ref FlatTokenParserState state, ref PreTokenDecorator decorator, ref string argument)
        {
            var next = enumerator.Next();

            if (string.IsNullOrWhiteSpace(argument) &&
                string.IsNullOrWhiteSpace(next))
            {
                return;
            }

            switch (next)
            {
                case ")":
                    decorator.Args.Add(argument.Trim());
                    argument = string.Empty;
                    state = FlatTokenParserState.InDecorator;
                    break;

                case "'":
                    if (string.IsNullOrWhiteSpace(argument))
                    {
                        argument = string.Empty;
                        state = FlatTokenParserState.InDecoratorArgumentSingleQuotes;
                    }
                    else
                    {
                        argument += next;
                    }
                    break;

                case @"""":
                    if (string.IsNullOrWhiteSpace(argument))
                    {
                        argument = string.Empty;
                        state = FlatTokenParserState.InDecoratorArgumentDoubleQuotes;
                    }
                    else
                    {
                        argument += next;
                    }
                    break;

                case ",":
                    decorator.Args.Add(argument.Trim());
                    argument = string.Empty;
                    state = FlatTokenParserState.InDecoratorArgument;
                    break;

                default:
                    argument += next;
                    break;
            }

        }

        private void ParseDecoratorArgumentInSingleQuotes(PreTokenEnumerator enumerator, ref FlatTokenParserState state, ref PreTokenDecorator decorator, ref string argument)
        {
            var next = enumerator.Next();

            switch (next)
            {
                case "'":
                    decorator.Args.Add(argument);
                    argument = string.Empty;
                    state = FlatTokenParserState.InDecoratorArgumentRunOff;
                    break;

                default:
                    argument += next;
                    break;
            }
        }

        private void ParseDecoratorArgumentInDoubleQuotes(PreTokenEnumerator enumerator, ref FlatTokenParserState state, ref PreTokenDecorator decorator, ref string argument)
        {
            var next = enumerator.Next();

            switch (next)
            {
                case @"""":
                    decorator.Args.Add(argument);
                    argument = string.Empty;
                    state = FlatTokenParserState.InDecoratorArgumentRunOff;
                    break;

                default:
                    argument += next;
                    break;
            }
        }

        private void ParseDecoratorArgumentRunOff(PreTokenEnumerator enumerator, ref FlatTokenParserState state)
        {
            var next = enumerator.Next();

            if (string.IsNullOrWhiteSpace(next)) return;

            switch (next)
            {
                case ",":
                    state = FlatTokenParserState.InDecoratorArgument;
                    break;

                case ")":
                    state = FlatTokenParserState.InDecorator;
                    break;

                default:
                    throw new TokenizerException($"Unexpected character: '{next}'"); 
            }

        }

        private void AppendToken(PreTemplate template, PreToken token)
        {
            token.Id = template.Tokens.Count + 1;

            var preamble = GetRepeatingMultilinePreamble(token);

            if (string.IsNullOrEmpty(preamble) == false && token.Repeating)
            {
                token.Repeating = false;
                template.Tokens.Add(token);

                var repeat = new PreToken
                {
                    Optional = true,
                    Repeating = true,
                    TerminateOnNewline = token.TerminateOnNewline
                };

                repeat.AppendName(token.Name);
                repeat.AppendPreamble(preamble);
                repeat.AppendDecorators(token.Decorators);

                repeat.Id = template.Tokens.Count + 1;
                repeat.DependsOnId = token.Id;
                template.Tokens.Add(repeat);
            }
            else
            {
                template.Tokens.Add(token);
            }
        }

        private void AppendDecorator(PreTokenEnumerator enumerator, PreToken token, PreTokenDecorator decorator)
        {
            if (decorator == null) return;
            if (string.IsNullOrEmpty(decorator.Name)) return;

            switch (decorator.Name.ToLowerInvariant())
            {
                case "eol":
                case "$":
                    if (decorator.Args.Any()) throw  new ParsingException($"'{decorator.Name}' decorator does not take any arguments", enumerator);
                    token.TerminateOnNewline = true;
                    break;

                case "optional":
                case "?":
                    if (decorator.Args.Any()) throw  new ParsingException($"'{decorator.Name}' decorator does not take any arguments", enumerator);
                    token.Optional = true;
                    break;

                case "repeating":
                case "*":
                    if (decorator.Args.Any()) throw  new ParsingException($"'{decorator.Name}' decorator does not take any arguments", enumerator);
                    token.Repeating = true;
                    break;

                case "required":
                case "!":
                    if (decorator.Args.Any()) throw  new ParsingException($"'{decorator.Name}' decorator does not take any arguments", enumerator);
                    token.Required = true;
                    break;

                default:
                    token.Decorators.Add(decorator);
                    break;
            }
        }

        private string GetRepeatingMultilinePreamble(PreToken token)
        {
            if (token.Repeating == false) return null;
            if (string.IsNullOrEmpty(token.Preamble)) return null;
            if (token.Preamble.IndexOf('\n') == -1) return null;

            var pre = token.Preamble.SubstringBeforeLastString("\n");
            var post = token.Preamble.SubstringAfterLastString("\n");

            if (string.IsNullOrWhiteSpace(pre) == false &&
                string.IsNullOrWhiteSpace(post))
            {
                return "\n" + post;
            }

            return null;
        }
    }

    internal enum FlatTokenParserState
    {
        AtStart,
        InFrontMatter,
        InFrontMatterOption,
        InFrontMatterOptionValue,
        InFrontMatterComment,
        InPreamble,
        InTokenName,
        InDecorator,
        InDecoratorArgument,
        InDecoratorArgumentSingleQuotes,
        InDecoratorArgumentDoubleQuotes,
        InDecoratorArgumentRunOff

    }
}
