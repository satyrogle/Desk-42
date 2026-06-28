// ============================================================
// DESK 42 — Desk42CLI
//
// Single text-command parser for development/QA tooling.
// Splits a raw input string into args and dispatches to a
// registered handler by the first token (the command name).
// No domain logic lives here — handlers own all game-state
// reads/writes. This file only routes.
//
// Usage:
//   Desk42CLI.Register("bsm", BsmCliTool.Run);
//   Desk42CLI.Execute("bsm set-state AGITATED");
// ============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Debugging
{
    public static class Desk42CLI
    {
        public delegate string CommandHandler(string[] args);

        private static readonly Dictionary<string, CommandHandler> _handlers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Register a handler for a top-level command name (e.g. "bsm", "entropy").</summary>
        public static void Register(string command, CommandHandler handler)
        {
            if (string.IsNullOrWhiteSpace(command) || handler == null) return;
            _handlers[command] = handler;
        }

        public static void Unregister(string command) => _handlers.Remove(command);

        /// <summary>
        /// Parse and dispatch a raw command line, e.g. "bsm set-state AGITATED".
        /// Returns a result string for display in whatever UI hosts the console.
        /// </summary>
        public static string Execute(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return "(empty command)";

            string[] tokens = rawInput.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = tokens[0];
            string[] args = new string[tokens.Length - 1];
            Array.Copy(tokens, 1, args, 0, args.Length);

            if (!_handlers.TryGetValue(command, out var handler))
                return $"Unknown command: '{command}'. Known: {string.Join(", ", _handlers.Keys)}";

            try
            {
                return handler(args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Desk42CLI] '{command}' threw: {ex}");
                return $"Error: {ex.Message}";
            }
        }
    }
}

#endif
