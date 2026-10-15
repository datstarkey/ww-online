// Headless check (Aurora patch 0022): for a process ID, prints its on-screen windows, all its windows,
// its activation policy and whether it is the frontmost application. Exit 0 only when it has no
// on-screen window, is not frontmost and has no Dock icon (activation policy other than regular).
//   swift native/tools/window_check.swift <pid>
import AppKit
import CoreGraphics

guard CommandLine.arguments.count == 2, let pid = Int32(CommandLine.arguments[1]) else {
    FileHandle.standardError.write("usage: window_check.swift <pid>\n".data(using: .utf8)!)
    exit(2)
}

func windows(_ option: CGWindowListOption) -> [[String: Any]] {
    let list = CGWindowListCopyWindowInfo(option, kCGNullWindowID) as? [[String: Any]] ?? []
    return list.filter { ($0[kCGWindowOwnerPID as String] as? Int32) == pid }
}

let onScreen = windows(.optionOnScreenOnly)
let all = windows(.optionAll)
let front = NSWorkspace.shared.frontmostApplication?.processIdentifier
let policy = NSRunningApplication(processIdentifier: pid)?.activationPolicy
let policyName: String
switch policy {
case .regular?: policyName = "regular (Dock icon)"
case .accessory?: policyName = "accessory"
case .prohibited?: policyName = "prohibited"
default: policyName = "none (not an application)"
}
print("pid \(pid): on-screen windows \(onScreen.count), all windows \(all.count), policy \(policyName), frontmost \(front == pid)")
for w in onScreen {
    print("  on-screen: \(w[kCGWindowName as String] ?? "") layer \(w[kCGWindowLayer as String] ?? "") bounds \(w[kCGWindowBounds as String] ?? "")")
}
exit(onScreen.isEmpty && front != pid && policy != .regular ? 0 : 1)
