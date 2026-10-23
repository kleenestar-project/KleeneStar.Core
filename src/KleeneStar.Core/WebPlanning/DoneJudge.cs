using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebPlanning
{
    /// <summary>
    /// Tells whether objects are done - their workflow state stands for done - for many objects
    /// at once.
    /// </summary>
    /// <remarks>
    /// Asking object by object builds the class's board context and reads the object's state
    /// each time; for the load of a team over a year of tickets that is minutes. The judge
    /// builds each class's context once and reads the states of all of its objects in one
    /// query, and answers with the same reading as the progress bars.
    /// </remarks>
    internal sealed class DoneJudge
    {
        private readonly Dictionary<Guid, (ObjectBoardClassContext Context, Dictionary<Guid, string> States)> _classes = [];
        private readonly IReadOnlyDictionary<Guid, StatusCategory> _categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);

        /// <summary>
        /// Determines whether an object is done.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns><see langword="true"/> when its state stands for done.</returns>
        public bool IsDone(ObjectEntity @object)
        {
            if (!_classes.TryGetValue(@object.ClassId, out var entry))
            {
                var @class = CoreHub.ClassManager.GetClass(@object.ClassId);
                var context = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
                var states = context?.WorkflowField is null
                    ? []
                    : CoreHub.ValueManager
                        .GetValues(new Query<Value>().WhereEquals(x => x.FieldId, context.WorkflowField.Id))
                        .GroupBy(x => x.ObjectId)
                        .ToDictionary(x => x.Key, x => x.First().Data);

                entry = (context, states);
                _classes[@object.ClassId] = entry;
            }

            var category = ObjectBoardProjection.ResolveCategory(entry.States.GetValueOrDefault(@object.Id), entry.Context, _categories);

            return ObjectBoardProjection.CategoryProgress(category) >= 100;
        }
    }
}
