
namespace Sitecore.Pipelines.HttpRequest
{
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Resources.Media;
  using Sitecore.Web;
  using System;
  /// <summary>
  /// Resolves aliases.
  /// </summary>
  public class AliasResolver : HttpRequestProcessor
	{
		/// <summary>
		/// Runs the processor.
		/// </summary>
		/// <param name="args">The arguments.</param>
		public override void Process(HttpRequestArgs args)
		{
			Assert.ArgumentNotNull(args, "args");
			if (!Settings.AliasesActive)
			{
				Tracer.Warning("Aliases are not active.");
				return;
			}
			Database database = Context.Database;
			if (database == null)
			{
				Tracer.Warning("There is no context database in AliasResover.");
				return;
			}
			Profiler.StartOperation("Resolve alias.");
			if (database.Aliases.Exists(args.LocalPath))
			{
				if (!this.ProcessItem(args))
				{
					if (this.ProcessExternalUrl(args))
					{
						string text = (Context.Page != null) ? Context.Page.FilePath : null;
						if (!string.IsNullOrEmpty(text) && WebUtil.IsExternalUrl(text))
						{
							this.RedirectToExternalLink(text);
						}
					}
				}
				else if (Context.Item != null && Context.Item.Paths.IsMediaItem && Context.Item.TemplateID != TemplateIDs.MediaFolder)
				{
					string mediaUrl = MediaManager.GetMediaUrl(Context.Item);
					if (!string.IsNullOrEmpty(mediaUrl))
					{
						string handler = HandlerUtil.GetHandler(mediaUrl);
						if (!string.IsNullOrEmpty(handler))
						{
							Context.Data.RawUrl = mediaUrl;
							args.HttpContext.RewritePath(handler, mediaUrl, args.Url.QueryString, true);
							args.AbortPipeline();
						}
					}
				}
			}
			Profiler.EndOperation();
		}

		/// <summary>
		/// Processes the external URL.
		/// </summary>
		/// <param name="args">The arguments.</param>
		private bool ProcessExternalUrl(HttpRequestArgs args)
		{
			string targetUrl = Context.Database.Aliases.GetTargetUrl(args.LocalPath);
			return targetUrl.Length > 0 && this.ProcessExternalUrl(targetUrl);
		}

		/// <summary>
		/// Processes the external URL.
		/// </summary>
		/// <param name="path">The path.</param>
		private bool ProcessExternalUrl(string path)
		{
			if (Context.Page.FilePath.Length > 0)
			{
				return false;
			}
			Context.Page.FilePath = path;
			return true;
		}

		/// <summary>
		/// Processes the item.
		/// </summary>
		/// <param name="args">The arguments.</param>
		/// <returns>The item.</returns>
		private bool ProcessItem(HttpRequestArgs args)
		{
			ID targetID = Context.Database.Aliases.GetTargetID(args.LocalPath);
			if (!targetID.IsNull)
			{
				Item item = args.GetItem(targetID);
				if (item != null)
				{
					this.ProcessItem(args, item);
				}
				return true;
			}
			Tracer.Error("An alias for \"" + args.LocalPath + "\" exists, but points to a non-existing item.");
			return false;
		}

		/// <summary>
		/// Processes the item.
		/// </summary>
		/// <param name="args">The arguments.</param>
		/// <param name="target">The target.</param>
		private void ProcessItem(HttpRequestArgs args, Item target)
		{
			if (Context.Item == null)
			{
				Context.Item = target;
				Tracer.Info(string.Concat(new object[]
				{
					"Using alias for \"",
					args.LocalPath,
					"\" which points to \"",
					target.ID,
					"\""
				}));
			}
		}

		/// <summary>
		/// Redirect to external link, using virtual to make it more easy for unit-test usage
		/// </summary>
		/// <param name="path">the URL of link</param>
		protected virtual void RedirectToExternalLink(string path)
		{
			WebUtil.Redirect(path);
		}
	}
}
