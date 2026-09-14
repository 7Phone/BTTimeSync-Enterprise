# BTTimeSync Development Roadmap

## Current Version

**v0.8.0**

Git Tag: `v0.8.0`

Current development branch:

`feature/v0.8.0-refactor`

---

# v0.8.0 — Architecture Refactor & Core Stability

Status: **Completed**

## Architecture

- [x] Bluetooth Discovery
- [x] Bluetooth Connector
- [x] Bluetooth Transport
- [x] ByteTransport abstraction
- [x] PacketTransport
- [x] BTSP Session
- [x] TimeSyncSampler
- [x] Console Application / System separation
- [x] Configuration abstraction

## Time Synchronization

- [x] Multiple-sample time synchronization
- [x] Median-based offset calculation
- [x] MAD-based outlier detection
- [x] Best-sample selection
- [x] Windows system clock adjustment
- [x] Post-synchronization verification
- [x] Automatic periodic synchronization

## Bluetooth Communication

- [x] RFCOMM Server
- [x] RFCOMM Client
- [x] BTTimeSync service UUID
- [x] BTSP Hello / HelloAck handshake
- [x] Real two-machine communication test

## Configuration

- [x] `appsettings.json`
- [x] Strongly typed configuration
- [x] Embedded configuration
- [x] Single-file deployment compatibility

## Testing

- [x] Core unit tests
- [x] Bluetooth tests
- [x] 28/28 automated tests passed
- [x] Real two-machine synchronization test
- [x] Windows Task Scheduler elevated execution test
- [x] Single-file executable test

## Deployment

- [x] Self-contained Windows x64 publish
- [x] Single-file Console executable
- [x] Server executable
- [x] Task Scheduler / highest privilege approach validated

---

# v1.0 — Personal Stable Release

Goal:

> Build a stable, long-running version for personal use.

## Phase 1 — Deployment

- [ ] Create Console deployment script
- [ ] Create Server deployment script
- [ ] Create uninstall script
- [ ] Standardize deployment directory
- [ ] Automatically create Windows Task Scheduler tasks
- [ ] Validate installation and uninstallation
- [ ] Create deployment documentation

## Phase 2 — Reliability

- [ ] Bluetooth automatic reconnect
- [ ] Recover from temporary Bluetooth disconnect
- [ ] Recover after Server restart
- [ ] Recover after Console restart
- [ ] Recover after Windows restart
- [ ] Improve retry and backoff behavior
- [ ] Handle unexpected Server shutdown
- [ ] Handle unexpected Console shutdown

## Phase 3 — Logging

- [ ] Persistent application logs
- [ ] Daily log files
- [ ] Synchronization result logging
- [ ] Connection / disconnection logging
- [ ] Error and exception logging
- [ ] Reconnect logging
- [ ] Log retention policy

## Phase 4 — Long-running Validation

- [ ] 24-hour continuous test
- [ ] 72-hour continuous test
- [ ] Monitor CPU usage
- [ ] Monitor memory usage
- [ ] Monitor handle/resource usage
- [ ] Verify repeated synchronization stability

## Phase 5 — v1.0 Release

- [ ] Final automated tests
- [ ] Final two-machine test
- [ ] Final deployment test
- [ ] Final documentation
- [ ] Update README
- [ ] Create `v1.0.0` Git tag
- [ ] Create GitHub Release

---

# v1.1 — Optional Improvements

Potential improvements after v1.0 has proven stable:

- [ ] Better console status display
- [ ] More detailed synchronization statistics
- [ ] Improved configuration validation
- [ ] More configurable synchronization intervals
- [ ] Improved diagnostic information
- [ ] Additional automated tests
- [ ] Performance optimization

Items in this section are not required for v1.0.

---

# v2.0 — Productization

Goal:

> Make BTTimeSync suitable for distribution to other users.

## First-use Experience

- [ ] Automatic BTTimeSync Server discovery
- [ ] First-time Bluetooth pairing assistance
- [ ] Investigate automatic Bluetooth pairing
- [ ] First-use configuration wizard
- [ ] Automatic connection setup

## User Interface

- [ ] GUI client
- [ ] Connection status
- [ ] Synchronization status
- [ ] Last synchronization result
- [ ] Error notifications
- [ ] Configuration interface

## Deployment

- [ ] One-click installer
- [ ] Automatic installation
- [ ] Automatic Task Scheduler configuration
- [ ] Upgrade mechanism
- [ ] Uninstall mechanism

## Advanced Features

- [ ] Multiple clients
- [ ] Multiple servers
- [ ] Server discovery
- [ ] Client authentication
- [ ] Communication security improvements
- [ ] Automatic update

## Background Operation

- [ ] Evaluate Windows Service architecture
- [ ] Fully background operation
- [ ] Automatic recovery
- [ ] Service health monitoring

---

# Version Strategy

## v0.8.0

Architecture and core functionality baseline.

## v1.0

Stable personal-use version.

Focus on:

- Reliability
- Automatic recovery
- Deployment
- Logging
- Long-running stability

## v1.1

Optional improvements after v1.0 stability.

## v2.0

Productization for distribution to other users.

Focus on:

- First-use experience
- Automatic Bluetooth setup
- GUI
- Installer
- Multi-user scenarios
- Background service
- Updates

---

# Development Principles

1. Keep changes small and buildable.
2. Prefer complete-file replacements for source changes.
3. Build after meaningful code changes.
4. Run automated tests after functional changes.
5. Perform real hardware testing when Bluetooth behavior is affected.
6. Commit only verified changes.
7. Push stable commits to the remote repository.
8. Keep released versions reproducible.
9. Avoid introducing v2.0 requirements into v1.0 unless necessary.
10. Maintain a clear Git history.

---

# Current Baseline

The current stable baseline is:

`v0.8.0`

All v1.0 development should be based on this tag.
