﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Owin;
using Simplify.DI;
using Simplify.Web.Core;
using Simplify.Web.Diagnostics;
using Simplify.Web.Modules;
using Simplify.Web.Modules.Data;
using Simplify.Web.Settings;

namespace Simplify.Web.Owin
{
	/// <summary>
	/// Catched exceptions delegate
	/// </summary>
	/// <param name="e">The e.</param>
	public delegate void ExceptionEventHandler(Exception e);

	/// <summary>
	/// HTTP requests trace delegate
	/// </summary>
	public delegate void TraceEventHandler(IOwinContext context);

	/// <summary>
	/// Simplify.Web engine root
	/// </summary>
	public class SimplifyWebOwinMiddleware : OwinMiddleware
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SimplifyWebOwinMiddleware"/> class.
		/// </summary>
		/// <param name="next">The next middleware.</param>
		public SimplifyWebOwinMiddleware(OwinMiddleware next)
			: base(next)
		{
		}

		/// <summary>
		/// Occurs when exception occured and catched by framework.
		/// </summary>
		public static event ExceptionEventHandler OnException;

		/// <summary>
		/// Occurs on each request.
		/// </summary>
		public static event TraceEventHandler OnTrace;

		/// <summary>
		/// Process an individual request.
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public override Task Invoke(IOwinContext context)
		{
			using (var scope = DIContainer.Current.BeginLifetimeScope())
			{
				try
				{
					// Starts execution measurement
					scope.Container.Resolve<IStopwatchProvider>().StartMeasurement();

					// Tracing

					var settings = scope.Container.Resolve<ISimplifyWebSettings>();

					if (settings.ConsoleTracing)
						TraceToConsole(context);

					OnTrace?.Invoke(context);

					// Setup providers

					var webContextProvider = scope.Container.Resolve<IWebContextProvider>();
					var languageManagerProvider = scope.Container.Resolve<ILanguageManagerProvider>();
					var templateFactory = scope.Container.Resolve<ITemplateFactory>();
					var fileReader = scope.Container.Resolve<IFileReader>();
					var stringTable = scope.Container.Resolve<IStringTable>();

					webContextProvider.Setup(context);
					languageManagerProvider.Setup(context);
					templateFactory.Setup();
					fileReader.Setup();
					stringTable.Setup();

					// Run request process pipeline

					var requestHandler = scope.Container.Resolve<IRequestHandler>();
					return requestHandler.ProcessRequest(scope.Container, context);
				}
				catch (Exception e)
				{
					try
					{
						ProcessOnException(e);
					}
					catch (Exception exception)
					{
						return
							context.Response.WriteAsync(ExceptionInfoPageGenerator.Generate(exception,
								scope.Container.Resolve<ISimplifyWebSettings>().HideExceptionDetails));
					}

					return
						context.Response.WriteAsync(ExceptionInfoPageGenerator.Generate(e,
							scope.Container.Resolve<ISimplifyWebSettings>().HideExceptionDetails));
				}
			}
		}

		internal static bool ProcessOnException(Exception e)
		{
			if (OnException == null) return false;

			OnException(e);
			return true;
		}

		private static void TraceToConsole(IOwinContext context)
		{
			Trace.WriteLine(
				$"[{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss:fff", CultureInfo.InvariantCulture)}] [{context.Request.Method}] {context.Request.Uri.AbsoluteUri}");
		}
	}
}