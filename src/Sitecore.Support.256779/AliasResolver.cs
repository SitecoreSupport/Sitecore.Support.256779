
namespace Sitecore.Support.Pipelines.HttpRequest
{
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Pipelines.HttpRequest;
  using Sitecore.Resources.Media;
  using Sitecore.Security.AccessControl;
  using Sitecore.SecurityModel;
  using Sitecore.Web;


  /// <summary>
  /// Resolves aliases.
  /// </summary>
  public class AliasResolver : Sitecore.Pipelines.HttpRequest.AliasResolver
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
      }
      else
      {
        Database database = Context.Database;
        if (database == null)
        {
          Tracer.Warning("There is no context database in AliasResover.");
        }
        else
        {
          Profiler.StartOperation("Resolve alias.");
          if (database.Aliases.Exists(args.LocalPath))
          {
            if (!this.ProcessItem(args))
            {
              if (this.ProcessExternalUrl(args))
              {
                string filePath = Context.Page?.FilePath;
                if (!string.IsNullOrEmpty(filePath) && WebUtil.IsExternalUrl(filePath))
                {
                  this.RedirectToExternalLink(filePath);
                }
              }
            }
            else if (((Context.Item != null) && Context.Item.Paths.IsMediaItem) && (Context.Item.TemplateID != TemplateIDs.MediaFolder))
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
      }
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
        Item target = this.GetItem(targetID, args);
        if (target != null)
        {
          this.ProcessItem(args, target);
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
    #region Sitecore.Support.256779
    public Item GetItem(ID itemID, HttpRequestArgs args)
    {
      Item item;
      Assert.ArgumentNotNull(itemID, "itemID");
      using (new SecurityDisabler())
      {
        item = Sitecore.Context.Database.Items[itemID];
      }
      if (item != null)
      {
        return this.ApplySecurity(item, args);
      }
      return null;
    }
    public Item ApplySecurity(Item item, HttpRequestArgs args)
    {
      Assert.ArgumentNotNull(item, "item");
      if (Security.AccessControl.AuthorizationManager.IsAllowed(item, AccessRight.ItemRead, Sitecore.Context.User))
      {
        return item;
      }
      Tracer.Warning("Permission denied for item: " + item.Paths.Path);
      args.PermissionDenied = true;

      args.Url.ItemPath = item.Uri.Path;
      return null;
    }
    #endregion
  }
}
