using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace RouteJs
{
	/// <summary>
	/// Gets information about the attribute routes in the site
	/// </summary>
	/// <remarks>
	/// Reference: https://github.com/aspnet/Mvc/blob/dev/src/Microsoft.AspNetCore.Mvc.Core/Routing/AttributeRoute.cs
	/// </remarks>
	public class AttributeRouteFetcher : IRouteFetcher
    {
		private readonly IInlineConstraintResolver _constraintResolver;
		private readonly IActionDescriptorCollectionProvider _actionDescriptorsCollectionProvider;
		private readonly IRouteTemplateParser _parser;
		private readonly IConstraintsProcessor _constraintsProcessor;

		/// <summary>
		/// Creates a new <see cref="AttributeRouteFetcher"/>.
		/// </summary>
		/// <param name="constraintResolver"></param>
		/// <param name="actionDescriptorsCollectionProvider"></param>
		/// <param name="parser"></param>
		/// <param name="constraintsProcessor"></param>
		public AttributeRouteFetcher(
			IInlineConstraintResolver constraintResolver,
			IActionDescriptorCollectionProvider actionDescriptorsCollectionProvider,
			IRouteTemplateParser parser, 
			IConstraintsProcessor constraintsProcessor
		)
		{
			_constraintResolver = constraintResolver;
			_actionDescriptorsCollectionProvider = actionDescriptorsCollectionProvider;
			_parser = parser;
			_constraintsProcessor = constraintsProcessor;
		}

		/// <summary>
		/// Gets the order of this route fetch relative to others. Fetches with smaller numbers
		/// will have their routes listed earlier in the overall route list.
		/// </summary>
		public int Order => 1;

		/// <summary>
		/// Gets the route information
		/// </summary>
		/// <param name="routeData">Raw route data from ASP.NET MVC</param>
		/// <returns>Processed route information</returns>
		public IEnumerable<RouteInfo> GetRoutes(RouteData routeData)
		{
			return _actionDescriptorsCollectionProvider.ActionDescriptors.Items
				.OfType<ControllerActionDescriptor>()
				.Where(a => a.AttributeRouteInfo?.Template != null)
				.Select(ProcessAttributeRoute)
				// Sort by Order then Precedence, same as InnerAttributeRoute
				.OrderBy(x => x.Order)
				.ThenBy(x => x.Precedence)
				.ThenBy(x => x.Url, StringComparer.Ordinal);
		}

		/// <summary>
		/// Processes one attribute route
		/// </summary>
		/// <param name="action">Action to process</param>
		/// <returns>Route information</returns>
		private AttributeRouteInfo ProcessAttributeRoute(ControllerActionDescriptor action)
		{
			var template = TemplateParser.Parse(action.AttributeRouteInfo.Template);

			var info = new AttributeRouteInfo
			{
				Constraints = GetConstraints(action.AttributeRouteInfo.Template, template),
                Defaults = GetDefaults(action, template),
                Optional = new List<string>(),
				Order = action.AttributeRouteInfo.Order,
				Precedence = RoutePrecedence.ComputeOutbound(template),
			};
			_parser.Parse(template, info);

			return info;
		}

		/// <summary>
		/// Processes the constraints for the specified template.
		/// </summary>
		/// <param name="rawTemplate">Raw template string</param>
		/// <param name="parsedTemplate">Parsed template</param>
		/// <returns>The constraints for this route</returns>
		private IDictionary<string, object> GetConstraints(string rawTemplate, RouteTemplate parsedTemplate)
		{
			var constraintBuilder = new RouteConstraintBuilder(_constraintResolver, rawTemplate);
			foreach (var parameter in parsedTemplate.Parameters)
			{
				if (parameter.InlineConstraints != null)
				{
					if (parameter.IsOptional)
					{
						constraintBuilder.SetOptional(parameter.Name);
					}

					foreach (var inlineConstraint in parameter.InlineConstraints)
					{
						constraintBuilder.AddResolvedConstraint(parameter.Name, inlineConstraint.Constraint);
					}
				}
			}

			var constraints = constraintBuilder.Build();
			return _constraintsProcessor.ProcessConstraints(constraints);
		}

		/// <summary>
		/// Processes the defaults for the specified action.
		/// </summary>
		/// <param name="action">MVC action</param>
		/// <param name="template">Route template for this action</param>
		/// <returns>Defaults for this action</returns>
		private IDictionary<string, object> GetDefaults(ControllerActionDescriptor action, RouteTemplate template)
		{
			var defaults = template.Parameters
				.Where(p => p.DefaultValue != null)
				.ToDictionary(
					p => p.Name.ToLowerInvariant(),
					p => p.DefaultValue,
					StringComparer.OrdinalIgnoreCase
				);

			defaults.Add("controller", action.ControllerName);
			defaults.Add("action", action.ActionName);
			return defaults;
		}

		private class AttributeRouteInfo : RouteInfo
		{
			/// <summary>
			/// Gets or sets the precedence of this route. This is used after <see cref="Order"/>
			/// to determine the ordering of the routes.
			/// </summary>
			public decimal Precedence { get; set; }
			/// <summary>
			/// Gets or sets the user-provided order of this route.
			/// </summary>
			public int Order { get; set; }
		}
    }
}
