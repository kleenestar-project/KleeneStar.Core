using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Impact._objectkey_
{
    /// <summary>
    /// The impact analysis of one object as a graph: what changing it touches, and along which
    /// relations. The URL is <c>/api/1/impact/{objectkey}</c>, with the number of steps to
    /// follow riding along as <c>?depth=</c>.
    /// </summary>
    /// <remarks>
    /// The walk itself belongs to <see cref="IObjectImpactManager"/> - which relations carry a
    /// consequence and in which direction is a statement about the domain, not about HTTP. What
    /// is left here is the projection into the shape a graph viewer reads, and the same
    /// permission check the relation surface of the object makes: an analysis is a reading of
    /// the relations of an object, so whoever may read those may read this.
    /// <para>
    /// Nodes and edges are answered from a single walk rather than from the two separate
    /// retrievals the base class offers, because they are two halves of one answer: running the
    /// analysis twice could, with a change landing in between, produce an edge whose end is not
    /// among the nodes.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.impact.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : RestApiGraph
    {
        /// <summary>
        /// The colour a node is painted in when its object carries no resolvable state - the
        /// neutral grey the workflow graph uses for the same case.
        /// </summary>
        private const string NeutralColor = "#6c757d";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the impact graph of the addressed object, once the caller may read its
        /// relations.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The response carrying the nodes and the edges.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                // a key naming no object - or naming one the caller is not cleared for - is not
                // an authorization decision to report, it is an empty canvas
                return new RestApiGraphResult() { Nodes = [], Edges = [] }.ToResponse();
            }

            if (!ObjectRelationAuthorization.MayRead(@object, request))
            {
                return new ResponseForbidden();
            }

            var result = CoreHub.ObjectImpactManager.Analyze(@object.Id, ResolveDepth(request));

            return new RestApiGraphResult()
            {
                Nodes = [.. result.Nodes.Select(x => ToNode(x, result.Origin, request))],
                Edges = [.. result.Edges.Select(ToEdge)]
            }
                .ToResponse();
        }

        /// <summary>
        /// Projects one reached object into a node: what it is called, what it is, and where it
        /// stands.
        /// </summary>
        /// <remarks>
        /// The three lines are what a reader needs to judge a consequence without opening the
        /// record - the key names it, the summary says what it is, the state says whether the
        /// connection still matters. The origin is painted in the accent so the reader sees at
        /// once whose impact they are looking at, the way the relation graph paints its subject.
        /// </remarks>
        /// <param name="node">The reached object and its distance.</param>
        /// <param name="origin">The object the analysis started from.</param>
        /// <param name="request">The incoming request, for the culture the labels are read in.</param>
        /// <returns>The node.</returns>
        private static RestApiGraphNode ToNode(ObjectImpactNode node, ObjectEntity origin, IRequest request)
        {
            var @object = node.Object;
            var isOrigin = origin is not null && @object.Id == origin.Id;
            var category = ResolveCategory(@object);

            return new RestApiGraphNode()
            {
                Id = @object.Id.ToString(),
                Label = @object.Key,
                Description = @object.Summary,
                State = category is null ? null : ObjectBoardProjection.CategoryLabel(category),
                StateColor = category?.Color ?? NeutralColor,
                Image = ObjectIcon.Uri(@object),
                Uri = ObjectKindCatalog.ResolveDetailUri(@object)?.ToString(),
                Shape = "rect",
                BackgroundColor = isOrigin ? "var(--wx-primary)" : null,
                ForegroundColor = isOrigin ? "#ffffff" : null
            };
        }

        /// <summary>
        /// Projects one step into an edge, labelled with what the relation does rather than with
        /// what it is called.
        /// </summary>
        /// <remarks>
        /// The graph answers a question about consequences, so the edge says which consequence it
        /// carries - <em>blocks</em>, <em>closes</em>, <em>rolls up</em> - and not the label of
        /// the relation, which the relation surface already shows and which reads backwards on
        /// half the edges here: an edge is drawn the way the consequence runs, and a relation
        /// whose source follows its target is stored the other way round.
        /// </remarks>
        /// <param name="edge">The step.</param>
        /// <returns>The edge.</returns>
        private static RestApiGraphEdge ToEdge(ObjectImpactEdge edge)
        {
            return new RestApiGraphEdge()
            {
                Id = edge.RelationId.ToString(),
                From = edge.From.ToString(),
                To = edge.To.ToString(),
                Label = I18N.Translate(EffectLabel(edge.Effect))
            };
        }

        /// <summary>
        /// Returns the internationalization key naming what an effect does to what follows it.
        /// </summary>
        /// <param name="effect">The effect of the relation the step was taken along.</param>
        /// <returns>The key.</returns>
        private static string EffectLabel(RelationEffect effect)
        {
            return effect switch
            {
                RelationEffect.BlocksCompletion => "kleenestar.core:object.impact.effect.blocks",
                RelationEffect.ClosesItem => "kleenestar.core:object.impact.effect.closes",
                RelationEffect.AggregatesProgress => "kleenestar.core:object.impact.effect.aggregates",
                _ => "kleenestar.core:object.impact.effect.none"
            };
        }

        /// <summary>
        /// Resolves the workflow state category of an object, which is what the node's state dot
        /// reports.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The category, or <see langword="null"/> when the object carries no
        /// resolvable state.</returns>
        private static StatusCategory ResolveCategory(ObjectEntity @object)
        {
            var @class = @object.Class ?? CoreHub.ClassManager.GetClass(@object.ClassId);

            if (@class is null)
            {
                return null;
            }

            var context = ObjectBoardProjection.BuildClassContext(@class);
            var categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);

            return ObjectBoardProjection.ResolveCategory(@object.Id, context, categories);
        }

        /// <summary>
        /// Resolves the object the route addresses, as the caller is allowed to see it.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static ObjectEntity ResolveObject(IRequest request)
        {
            return CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>());
        }

        /// <summary>
        /// Reads how many steps the caller asked for.
        /// </summary>
        /// <remarks>
        /// An absent or unreadable value is the default rather than an error: the depth is a
        /// reading preference, and a graph is still an answer at any of them. The manager clamps
        /// what it is given, so a caller cannot walk the whole installation by asking for a
        /// thousand.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>The requested depth.</returns>
        private static int ResolveDepth(IRequest request)
        {
            var value = request?.GetParameter("depth")?.Value;

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : ObjectImpactManager.DefaultDepth;
        }
    }
}
