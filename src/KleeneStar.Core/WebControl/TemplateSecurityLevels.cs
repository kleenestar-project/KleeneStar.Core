using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The security levels a template gives the objects created from it, one choice per served
    /// class, stored as the preset <see cref="PresetKey"/> of the template.
    /// </summary>
    /// <remarks>
    /// A security level belongs to one class, so the choice is made per class. The preset holds
    /// the choices separated by semicolons: the id of a level (which names its class), or
    /// <c>none@{class id}</c> for an unclassified object of that class. A class without a choice
    /// takes the default level of the class. The empty guid as the whole value leaves the objects
    /// of every class unclassified.
    /// <para>
    /// The wizard step posts one tile selection per served class, named
    /// <see cref="FieldPrefix"/><c>{class id}</c>, with the value <see cref="DefaultValue"/>, the
    /// empty guid or a level id.
    /// </para>
    /// </remarks>
    public static class TemplateSecurityLevels
    {
        /// <summary>
        /// The preset key the choices are stored under.
        /// </summary>
        public const string PresetKey = nameof(ObjectEntity.SecurityLevelId);

        /// <summary>
        /// The prefix of the payload fields of the wizard step, followed by the class id.
        /// </summary>
        public const string FieldPrefix = "SecurityLevel_";

        /// <summary>
        /// The value of the tile that leaves the class on its default level.
        /// </summary>
        public const string DefaultValue = "default";

        /// <summary>
        /// The prefix of the token that leaves the objects of a class unclassified.
        /// </summary>
        private const string NonePrefix = "none@";

        /// <summary>
        /// Reads the choices of a stored preset.
        /// </summary>
        /// <param name="value">The preset value.</param>
        /// <returns>
        /// The choice per class id: a level id, or null for unclassified. The empty guid as key
        /// stands for every class.
        /// </returns>
        public static IReadOnlyDictionary<Guid, Guid?> Parse(string value)
        {
            var choices = new Dictionary<Guid, Guid?>();

            foreach (var token in (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.StartsWith(NonePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (Guid.TryParse(token[NonePrefix.Length..], out var classId) && classId != Guid.Empty)
                    {
                        choices[classId] = null;
                    }

                    continue;
                }

                if (!Guid.TryParse(token, out var levelId))
                {
                    continue;
                }

                if (levelId == Guid.Empty)
                {
                    choices[Guid.Empty] = null;
                    continue;
                }

                if (CoreHub.SecurityLevelManager?.GetSecurityLevel(levelId) is { } level)
                {
                    choices[level.ClassId] = level.Id;
                }
            }

            return choices;
        }

        /// <summary>
        /// Writes choices as a preset value.
        /// </summary>
        /// <param name="choices">The choice per class id: a level id, or null for unclassified.</param>
        /// <returns>The preset value, or null when no class has a choice.</returns>
        public static string Format(IReadOnlyDictionary<Guid, Guid?> choices)
        {
            var tokens = (choices ?? new Dictionary<Guid, Guid?>())
                .Where(x => x.Key != Guid.Empty)
                .Select(x => x.Value is Guid level && level != Guid.Empty ? level.ToString() : NonePrefix + x.Key)
                .ToList();

            return tokens.Count == 0 ? null : string.Join(";", tokens);
        }

        /// <summary>
        /// Resolves the choice of a template's presets for a class.
        /// </summary>
        /// <param name="presets">The presets of the template.</param>
        /// <param name="classId">The class the object is created in.</param>
        /// <param name="level">The chosen level, or null for unclassified.</param>
        /// <returns>True when the template chose for the class; false for its default level.</returns>
        public static bool TryResolve(IReadOnlyDictionary<string, string> presets, Guid classId, out Guid? level)
        {
            level = null;

            if (presets is null || !presets.TryGetValue(PresetKey, out var value))
            {
                return false;
            }

            var choices = Parse(value);

            return choices.TryGetValue(classId, out level) || choices.TryGetValue(Guid.Empty, out level);
        }

        /// <summary>
        /// Writes the resolved choice for one class into a copy of the presets, as the object
        /// create wizard reads it: a level id, the empty guid for unclassified, or no entry for
        /// the class default.
        /// </summary>
        /// <param name="presets">The presets of the template.</param>
        /// <param name="classId">The class the object is created in.</param>
        /// <returns>The presets for the class.</returns>
        public static IReadOnlyDictionary<string, string> ForClass(IReadOnlyDictionary<string, string> presets, Guid classId)
        {
            var result = new Dictionary<string, string>(presets ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

            result.Remove(PresetKey);

            if (TryResolve(presets, classId, out var level))
            {
                result[PresetKey] = (level ?? Guid.Empty).ToString();
            }

            return result;
        }

        /// <summary>
        /// Determines whether a payload carries the fields of the wizard step.
        /// </summary>
        /// <param name="keys">The payload keys.</param>
        /// <returns>True when at least one class was answered.</returns>
        public static bool Carries(IEnumerable<string> keys)
        {
            return (keys ?? []).Any(x => x.StartsWith(FieldPrefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads the answer the payload gives for a class.
        /// </summary>
        /// <param name="payload">The payload; keys are matched ignoring case.</param>
        /// <param name="classId">The class.</param>
        /// <param name="answer">The answer: <see cref="DefaultValue"/>, the empty guid or a level id.</param>
        /// <returns>True when the payload answers for the class.</returns>
        public static bool TryReadAnswer(IReadOnlyDictionary<string, object> payload, Guid classId, out string answer)
        {
            answer = null;

            var entry = (payload ?? new Dictionary<string, object>())
                .FirstOrDefault(x => string.Equals(x.Key, FieldPrefix + classId, StringComparison.OrdinalIgnoreCase));

            if (entry.Key is null)
            {
                return false;
            }

            answer = entry.Value switch
            {
                null => string.Empty,
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                JsonElement element => element.ToString(),
                _ => entry.Value.ToString()
            };
            answer = answer?.Trim() ?? string.Empty;

            return true;
        }

        /// <summary>
        /// Computes the choices a payload stores: every served class the payload answers takes
        /// the answer; a class it does not answer keeps the stored choice.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="classIds">The served classes.</param>
        /// <param name="stored">The stored preset value.</param>
        /// <returns>The preset value, or null when no class has a choice.</returns>
        public static string Collect(IReadOnlyDictionary<string, object> payload, IEnumerable<Guid> classIds, string stored)
        {
            var previous = Parse(stored);
            var choices = new Dictionary<Guid, Guid?>();

            foreach (var classId in (classIds ?? []).Distinct())
            {
                if (TryReadAnswer(payload, classId, out var answer))
                {
                    if (Guid.TryParse(answer, out var levelId))
                    {
                        choices[classId] = levelId == Guid.Empty ? null : levelId;
                    }

                    continue;
                }

                if (previous.TryGetValue(classId, out var kept))
                {
                    choices[classId] = kept;
                }
            }

            return Format(choices);
        }

        /// <summary>
        /// Writes a preset value into serialized presets.
        /// </summary>
        /// <param name="presets">The serialized presets, or null.</param>
        /// <param name="value">The value of <see cref="PresetKey"/>, or null to remove it.</param>
        /// <returns>The serialized presets, or null when none remain.</returns>
        public static string Store(string presets, string value)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(presets))
            {
                try
                {
                    foreach (var (key, element) in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(presets) ?? [])
                    {
                        map[key] = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
                    }
                }
                catch (JsonException)
                {
                    // malformed presets are replaced by what can be written now
                }
            }

            map.Remove(PresetKey);

            if (!string.IsNullOrWhiteSpace(value))
            {
                map[PresetKey] = value;
            }

            return map.Count == 0 ? null : JsonSerializer.Serialize(map);
        }
    }
}
