# Changelog

All notable changes to HoneyDrunk.Notify.Queue.AzureStorage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

- Host configuration now treats `NotifyQueueConnection` as the flat Node-internal Azure Storage Queue connection setting.

## [0.1.0] - 2026-01-01

### Added

- Initial Azure Storage Queue implementation for Notify dispatch.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0
