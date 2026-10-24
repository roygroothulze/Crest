import SwiftUI

/// Whether a Space keeps its own website data or browses with another
/// Space's, so sign-ins carry over between them. Private browsing never
/// shares, so the section is left out there.
struct BrowserSpaceDataSharingSection: View {
    let browser: BrowserStore
    let space: SpaceModel

    @State private var errorMessage: String?

    var body: some View {
        if !browser.isPrivateBrowsing {
            Section {
                Picker("Share Data With", selection: hostBinding) {
                    Text("None").tag(UUID?.none)
                    ForEach(hosts) { host in
                        Text(host.settings.name).tag(UUID?.some(host.id))
                    }
                }
                .disabled(space.settings.requiresAuthentication)
                .accessibilityIdentifier("space-data-sharing-picker")

                if let errorMessage {
                    Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                        .font(.footnote)
                        .foregroundStyle(.red)
                        .accessibilityIdentifier("space-data-sharing-error")
                }
            } header: {
                Text("Website Data")
            } footer: {
                Text(footer).crestFormFootnote()
            }
        }
    }

    // MARK: - Hosts

    /// The Spaces this one may browse with: every other open Space that keeps
    /// its own data, plus the one it shares with now.
    private var hosts: [SpaceModel] {
        browser.spaceModels.filter { candidate in
            candidate.id != space.id
                && (candidate.id == space.settings.sharesDataWith
                    || candidate.settings.sharesDataWith == nil && !candidate.settings.requiresAuthentication)
        }
    }

    /// The Spaces that browse with this one's data.
    private var guests: [SpaceModel] {
        browser.spaceModels.filter { $0.settings.sharesDataWith == space.id }
    }

    private var hostBinding: Binding<UUID?> {
        Binding {
            space.settings.sharesDataWith
        } set: { hostID in
            errorMessage = browser.setDataSharing(of: space.id, with: hostID)?.explanation
        }
    }

    private var footer: String {
        if space.settings.requiresAuthentication {
            return String(localized: "Locked Spaces keep their own data.")
        }
        if let hostID = space.settings.sharesDataWith, let host = browser.spaceModel(hostID) {
            return String(localized: "Uses sign-ins and site data from “\(host.settings.name)”.")
        }
        if !guests.isEmpty {
            let names = guests.map(\.settings.name).formatted(.list(type: .and))
            return String(localized: "\(names) use this Space’s sign-ins and site data.")
        }
        return String(localized: "Keeps its own sign-ins and site data.")
    }
}
