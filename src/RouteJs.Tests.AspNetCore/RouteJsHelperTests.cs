using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace RouteJs.Tests.AspNetCore
{
	/// <summary>
	/// Unit tests for <see cref="RouteJsHelper" />.
	/// </summary>
    public class RouteJsHelperTests
    {
		[Theory]
		[InlineData("HelloWorld", "Development", "/_routejs/db8ac1c259eb89d4a131b253bacfca5f319d54f2/")]
		[InlineData("HelloWorld", "Production", "/_routejs/db8ac1c259eb89d4a131b253bacfca5f319d54f2/min")]
		[InlineData("HelloWorld2", "Development", "/_routejs/ebd813c9fb137372648f5367af2372f918c0397f/")]
		[InlineData("HelloWorld2", "Production", "/_routejs/ebd813c9fb137372648f5367af2372f918c0397f/min")]
		public void BuildsUrlBasedOnOutputAndEnvironment(
			string scriptOutput, 
			string environment,
			string expectedUrl
		)
		{
			var urlHelper = new Mock<IUrlHelper>();
			urlHelper.Setup(x => x.Action(It.IsAny<UrlActionContext>())).Returns((UrlActionContext context) =>
			{
				var values = HtmlHelper.ObjectToDictionary(context.Values);
				// Super simple URL building, purely for test purposes
				var url = "/_routejs";
				if (values.ContainsKey("hash"))
				{
					url += "/" + values["hash"];
					if (values.ContainsKey("environment"))
					{
						url += "/" + values["environment"];
					}
				}
				return url;
			});
			var urlHelperFactory = new Mock<IUrlHelperFactory>();
			urlHelperFactory.Setup(x => x.GetUrlHelper(It.IsAny<ActionContext>())).Returns(urlHelper.Object);

			var routeJs = new Mock<IRouteJs>();
			routeJs.Setup(x => x.GetJavaScript(It.IsAny<bool>())).Returns(scriptOutput);
			var serviceProvider = new Mock<IServiceProvider>();
			serviceProvider.Setup(x => x.GetService(typeof (IRouteJs))).Returns(routeJs.Object);
			var hostingEnvironment = new Mock<IHostingEnvironment>();
			hostingEnvironment.Setup(x => x.EnvironmentName).Returns(environment);
			var actionContextAccessor = new Mock<IActionContextAccessor>();

		    var helper = new RouteJsHelper(
				urlHelperFactory.Object,
				serviceProvider.Object, 
				hostingEnvironment.Object,
				actionContextAccessor.Object
			);

			RouteJsHelper.ClearCache();
			var result = helper.Render();
			Assert.Equal($"<script src=\"{expectedUrl}\"></script>", result.ToString());
		}
    }
}
