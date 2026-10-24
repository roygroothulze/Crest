using System.Text.Json.Nodes;

using CrestCore.Application;
using CrestCore.Contracts;
using CrestCore.Domain;

using Xunit;

namespace CrestCore.Tests;

/// A Space keeps its own website data unless it chooses to browse with
/// another Space's: a stored Space without the choice stays isolated, pages
/// open in the profile the choice names, a change moves open pages to it, and
/// private browsing, a lock or the other Space's deletion keeps each Space's
/// own.
public sealed partial class BrowserContractsTests {
    /// A saved session of three Spaces, each with its own profile, where the
    /// second shares the first's website data when `shares`.
    private static JsonNode ThreeSpaceSession(bool shares = false) {
        var session = TwoSpaceSession();
        session["spaces"]!.AsArray().Add(SavedSession().Document["session"]!["spaces"]![0]!.DeepClone());
        if (shares) session["spaces"]![1]!["sharesDataWith"] = SpaceId(session["spaces"]![0]!).ToString("D").ToUpperInvariant();
        return session;
    }

    private static Guid CreatedProfile(RecordingEngine binding) => Assert.IsType<CreatePage>(binding.Commands[^1]).ProfileId;

    [Fact]
    public void AStoredSpaceStaysIsolatedUntilItSharesAndKeepsItsChoiceAcrossASave() {
        var session = ThreeSpaceSession();
        var stored = StoredSessionCodec.DecodeSession(session);
        Assert.All(stored.Spaces, space => Assert.Null(space.Settings.SharesDataWith));

        var host = stored.Spaces[0].Id;
        var sharing = stored with {
            Spaces = [stored.Spaces[0], stored.Spaces[1] with { Settings = stored.Spaces[1].Settings with { SharesDataWith = host } },
                stored.Spaces[2]]
        };
        var reread = StoredSessionCodec.DecodeSession(StoredSessionCodec.Encode(sharing));
        Assert.Equal(host, reread.Spaces[1].Settings.SharesDataWith);
        Assert.Null(reread.Spaces[2].Settings.SharesDataWith);

        // A value this build cannot read keeps the Space's own data.
        session["spaces"]![1]!["sharesDataWith"] = "not an identity";
        Assert.Null(StoredSessionCodec.DecodeSession(session).Spaces[1].Settings.SharesDataWith);
    }

    [Fact]
    public void AGuestsPagesBrowseWithItsHostsProfileAndMoveBackWhenItStopsSharing() {
        var session = ThreeSpaceSession();
        var (host, guest, third) = (session["spaces"]![0]!, session["spaces"]![1]!, session["spaces"]![2]!);
        var (app, engine, binding, workspace, window) = PageHost(session);
        using var disposal = app;

        app.Send(new SetSpaceDataSharing(workspace, SpaceId(guest), SpaceId(host)));
        var page = Guid.NewGuid();
        app.Send(new OpenPage(page, workspace, SpaceId(guest), TabId(guest, 0), window));
        Assert.Equal(ProfileId(host), CreatedProfile(binding));
        app.Report(engine, new PageCreated(page));

        // Joining a Space that shares joins the Space whose data it uses.
        app.Send(new SetSpaceDataSharing(workspace, SpaceId(third), SpaceId(guest)));
        Assert.Equal(SpaceId(host), app.Workspace(workspace).Current.Spaces[2].Settings.SharesDataWith);

        // Stopping unloads the page, and the tab's next page opens in the
        // Space's own profile.
        var changes = app.Send(new SetSpaceDataSharing(workspace, SpaceId(guest), HostSpaceId: null));
        Assert.Contains(changes, change => change is PageUnloaded unloaded && unloaded.PageId == page);
        app.Send(new OpenPage(Guid.NewGuid(), workspace, SpaceId(guest), TabId(guest, 0), window));
        Assert.Equal(ProfileId(guest), CreatedProfile(binding));
    }

    [Fact]
    public void PrivateBrowsingALockOrTheHostsDeletionKeepEachSpacesOwnData() {
        var shared = ThreeSpaceSession(shares: true);
        var (host, guest) = (shared["spaces"]![0]!, shared["spaces"]![1]!);

        var (privateApp, _, privateBinding, privateWorkspace, privateWindow) = PageHost(shared.DeepClone(), WorkspaceKind.Private);
        using (privateApp) {
            privateApp.Send(new OpenPage(Guid.NewGuid(), privateWorkspace, SpaceId(guest), TabId(guest, 0), privateWindow));
            Assert.Equal(ProfileId(guest), CreatedProfile(privateBinding));
            Assert.IsType<DataSharingUnavailable>(Refusal(privateApp,
                new SetSpaceDataSharing(privateWorkspace, SpaceId(guest), SpaceId(host))));
        }

        var locked = shared.DeepClone();
        locked["spaces"]![0]!["accessPolicy"] = "deviceOwnerAuthentication";
        var (lockedApp, _, lockedBinding, lockedWorkspace, lockedWindow) = PageHost(locked);
        using (lockedApp) {
            lockedApp.Send(new OpenPage(Guid.NewGuid(), lockedWorkspace, SpaceId(guest), TabId(guest, 0), lockedWindow));
            Assert.Equal(ProfileId(guest), CreatedProfile(lockedBinding));
        }

        var (app, _, binding, workspace, window) = PageHost(shared);
        using var disposal = app;
        app.Send(new BeginDeletingSpace(workspace, window, SpaceId(host), Guid.NewGuid()));
        Assert.Null(app.Workspace(workspace).Current.Spaces[1].Settings.SharesDataWith);
        app.Send(new OpenPage(Guid.NewGuid(), workspace, SpaceId(guest), TabId(guest, 0), window));
        Assert.Equal(ProfileId(guest), CreatedProfile(binding));
    }
}
