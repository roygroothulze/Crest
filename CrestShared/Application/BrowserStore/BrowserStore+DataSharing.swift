import Foundation

extension BrowserStore {
    // MARK: - Actions - Data sharing

    /// Has the Space `spaceID` browse with the website data of the Space
    /// `hostID`, or keep its own again when `hostID` is nil, through the
    /// workspace that owns its profile. Answers the rule that refused it, or
    /// nil once the core took it.
    @discardableResult
    func setDataSharing(of spaceID: UUID, with hostID: UUID?) -> Rejection? {
        let owner = profileSettingsBrowser
        do throws(Rejection) {
            try owner.family.commit(
                SetSpaceDataSharing(workspaceID: owner.family.workspaceID, spaceID: spaceID, hostSpaceID: hostID),
                from: owner)
            return nil
        } catch {
            return error
        }
    }
}
