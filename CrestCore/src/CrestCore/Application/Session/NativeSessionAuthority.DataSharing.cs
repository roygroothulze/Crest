using CrestCore.Contracts;

namespace CrestCore.Application;

public sealed partial class NativeSessionAuthority {
    #region Actions - Data sharing

    /// The profile whose website data the pages of `space` browse with: the
    /// profile of the Space it shares data with, while `DataHost` finds one,
    /// and otherwise its own. Private browsing always keeps a Space's own. A
    /// borrowed workspace asks the workspace it borrows from, which holds the
    /// Space it shares with.
    internal Guid DataProfileOf(SpaceState space) {
        ArgumentNullException.ThrowIfNull(space);
        lock (Gate) {
            if (borrowedSource is { } source) return source.DataProfileOf(space);
            return privateBrowsing ? space.ProfileId : DataHost(session, space)?.ProfileId ?? space.ProfileId;
        }
    }

    /// The Space of `session` whose website data `space` browses with, or null
    /// when it keeps its own: it names none, or the one it names is gone, is
    /// being deleted, or shares another's data itself. A Space that asks for
    /// authentication neither shares nor is shared with, since its sign-ins
    /// would reach a Space that opens without unlocking it.
    internal static SpaceState? DataHost(SessionState session, SpaceState space) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(space);
        if (space.Settings.SharesDataWith is not { } hostId || hostId == space.Id || space.Settings.RequiresAuthentication)
            return null;
        var host = session.Spaces.FirstOrDefault(candidate => candidate.Id == hostId);
        return host is null || host.Settings.SharesDataWith is not null || host.Settings.RequiresAuthentication
            || session.SpaceDeletions.Any(deletion => deletion.SpaceId == hostId)
                ? null
                : host;
    }

    #endregion
}
