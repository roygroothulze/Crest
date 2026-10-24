using CrestCore.Application;
using CrestCore.Domain;

namespace CrestCore.Contracts;

#region Rejections

/// A `SetSpaceDataSharing` asked for sharing that cannot be: in private
/// browsing, between a Space and itself or a Space that shares its data, or
/// with a Space either of which asks for authentication.
public sealed record DataSharingUnavailable(Guid SpaceId) : Rejection {
    #region Variables

    /// What the person is told.
    [Localized]
    public string Message => "This Space can’t share browsing data with that Space.";

    #endregion
}

#endregion

/// Sets the Space `HostSpaceId` names as the one whose website data the Space
/// `SpaceId` browses with on this device, or with none, has it keep its own
/// again. A Space joins the Space the named one shares with, so every group
/// has one Space whose data it uses, and the Spaces that shared this one's
/// data follow it there. The Space's own data stays in its profile, unread
/// while it shares and back when it stops. Its tab pages unload keeping their
/// state, and come back in the profile they browse with now.
public sealed record SetSpaceDataSharing(Guid WorkspaceId, Guid SpaceId, Guid? HostSpaceId) : SessionIntent(WorkspaceId) {
    #region Actions - Session

    /// The choice stays on this device, as the website data it shares does,
    /// so no journal reads the edit.
    internal override SessionEdit? Edit(NativeSessionAuthority workspace, SessionTurn turn) {
        workspace.RequireOwnedSpaces();
        if (workspace.IsPrivateBrowsing) throw new Rejected(new DataSharingUnavailable(SpaceId));
        var space = workspace.Editable(turn.Basis, SpaceId);
        if (HostSpaceId is not { } named) {
            var isolated = workspace.Configured(space, space.Settings with { SharesDataWith = null });
            return new(NativeSessionAuthority.Replacing(turn.Basis, isolated), Staging: null);
        }
        var host = workspace.Editable(turn.Basis, named);
        if (host.Settings.SharesDataWith is { } root) host = workspace.Editable(turn.Basis, root);
        if (host.Id == space.Id || space.Settings.RequiresAuthentication || host.Settings.RequiresAuthentication)
            throw new Rejected(new DataSharingUnavailable(SpaceId));
        return new(turn.Basis with {
            Spaces = [.. turn.Basis.Spaces.Select(candidate => candidate.Id == space.Id || candidate.Settings.SharesDataWith == space.Id
                ? workspace.Configured(candidate, candidate.Settings with { SharesDataWith = host.Id })
                : candidate)]
        }, Staging: null);
    }

    #endregion

    #region Actions - Routing

    /// Every tab page whose Space now browses with another profile's data
    /// unloads keeping its state, in each workspace that shows the Space, and
    /// its tab's next page opens in the profile the Space uses now.
    internal override void Commit(CrestApp app, ChangeFeed changes) {
        base.Commit(app, changes);
        foreach (var page in app.Pages.All.Where(page => page.TabId is not null && Strayed(app, page)).ToArray()) {
            app.Pages.Remove(page.Id);
            changes.Publish(new PageRemoved(page.Id));
            changes.Publish(new PageUnloaded(page.Id, page.WorkspaceId, page.TabId!.Value));
            app.Pages.Close(page, keepsState: true, app.Issue);
        }
    }

    /// Whether `page` browses with a profile other than the one its Space
    /// uses now.
    private static bool Strayed(CrestApp app, Page page) =>
        app.Device.Attached(page.WorkspaceId) is { } workspace
        && workspace.Current.Spaces.FirstOrDefault(space => space.Id == page.SpaceId) is { } space
        && workspace.DataProfileOf(space) != page.ProfileId;

    #endregion
}
