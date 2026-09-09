using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing classes, including adding, retrieving, and removing, as well as
    /// handling class-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing classes and events for tracking changes 
    /// to the class collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public sealed class ClassManager : IClassManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an class is added.
        /// </summary>
        public event EventHandler<Class> ClassAdded;

        /// <summary>
        /// An event that fires when an class is udpated.
        /// </summary>
        public event EventHandler<Class> ClassUpdated;

        /// <summary>
        /// An event that fires when an class is removed.
        /// </summary>
        public event EventHandler<Class> ClassRemoved;

        /// <summary>
        /// Gets the collection of class names that are reserved and cannot be used for custom classes.
        /// </summary>
        /// <remarks>
        /// The reserved names typically represent system-defined routes and are not available
        /// for user-defined or custom class creation.
        /// </remarks>
        public static IEnumerable<string> ReservedClassNames =>
        [
            "default", "admin", "system", "assets", "api", "workspace",
            "workspaces", "icons", "setting"
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ClassManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a class based on its id.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>The class.</returns>
        public Class GetClass(Guid classId)
        {
            var query = new Query<Class>()
                .Where(x => x.Id == classId)
                .WithPaging(0, 1);

            return ModelHub.GetClasses(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a class based on its id.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>The class.</returns>
        public Class GetClass(ClassIdParameter classId)
        {
            var guid = Guid.TryParse(classId.Value, out Guid id) ? id : Guid.Empty;

            return GetClass(guid);
        }

        /// <summary>
        /// Retrieves a collection of classes that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned classes. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of classes that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Class> GetClasses(IQuery<Class> query)
        {
            return ModelHub.GetClasses(query);
        }

        /// <summary>
        /// Retrieves a collection of classes that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned classes. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of classes that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Class> GetClasses(IQuery<Class> query, IQueryContext context)
        {
            return ModelHub.GetClasses(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds a class to the manager.
        /// </summary>
        /// <param name="classEntity">The class to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IClassManager Add(Class classEntity)
        {
            ArgumentNullException.ThrowIfNull(classEntity);

            classEntity.Kind = Model.Entities.ObjectKind.Normalize(classEntity.Kind);
            classEntity.Renderer = Model.Entities.ObjectRenderer.Normalize(classEntity.Renderer);

            ModelHub.Add(classEntity);

            ClassAdded?.Invoke(this, classEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.class.created", classEntity);

            return this;
        }

        /// <summary>
        /// Update a class to the manager.
        /// </summary>
        /// <param name="classEntity">The class to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IClassManager Update(Class classEntity)
        {
            ArgumentNullException.ThrowIfNull(classEntity);

            classEntity.Kind = Model.Entities.ObjectKind.Normalize(classEntity.Kind);

            // an empty renderer is the state 'follow the kind' rather than a key, so it is
            // normalized to null instead of to a default - unlike the kind beside it
            classEntity.Renderer = Model.Entities.ObjectRenderer.Normalize(classEntity.Renderer);

            ModelHub.Update(classEntity);

            // the class is the single source of the object kind: stamp the (possibly
            // changed) kind onto every object of the class so the kind overviews
            // immediately reflect the change
            ModelHub.AlignObjectKinds(classEntity.Id, classEntity.Kind);

            ClassUpdated?.Invoke(this, classEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.class.updated", classEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified class from the manager.
        /// </summary>
        /// <remarks>This method removes the specified class from the manager. If the class does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="classId">The class id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IClassManager Remove(Guid classId)
        {
            var classEntry = GetClass(classId);

            if (classEntry is not null)
            {
                ModelHub.Remove(classEntry);
                ClassRemoved?.Invoke(this, classEntry);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
