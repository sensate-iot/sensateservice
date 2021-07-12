# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Logging statements in the composite router
- Name property on the `IRouter` interface

### Updated
- Routing cache
- Composite routing rules
- Routing code quality

### Removed
- Unused code

## [1.7.3] - 10-07-2021
### Added
- Additional logging

### Updated
- Improved the routing cache
- Improved routing service code quality
- Improved the composite router

### Removed
- Unused code

## [1.7.2] - 03-07-2021
### Added
- Additional validation/checks

### Updated
- Message routers
- Project dependencies
- Error handling

### Removed
- Incorrectly thrown exceptions

## [1.7.1] - 27-05-2021
### Updated
- Project dependencies
- Unit test project

### Removed
- Unused references/dependencies

## [1.7.0] - 24-05-2021
### Added
- Composite router implementation
- Routers for functional domains
- Message router abstractions (IRouter and IMessageRouter)
- Composite router tests
- Trigger router tests

### Updated
- Router metrics
- Routing logic
- Routing service

### Removed
- Migrated projects not related to message routing
- Unused code

## [1.6.2] - 13-04-2021
### Added
- Subscription count tracking
- Log scopes to the Live Data Service log template
- Subscription management service in the Live Data Service

### Updated
- Project dependency's
- Live Data Service log statement template

## [1.6.1] - 04-04-2021
### Added
- Exception handing to services
- Fatal to the background service when exceptions are not handled
- Timestamps to the live data service log statements

### Updated
- Access modifier of the background servic execute method
- Live data service logging statements
- Project dependency's

## [1.6.0] - 16-03-2021
### Added
- Caching of trigger actions in the Trigger Service
- Additional statistics types

### Updated
- Statistics logging
- Logging in the Storage Service
- Router initialization routine
- The database function `generic_getblobs`

### Removed
- Unused code
- TriggerInvocations table
- TriggerInvocation pgsql functions

## [1.5.0] - 11-03-2021
### Added
- Ability to disable data reloads in the message router
- Sensor commands when removing a user

### Updated
- Trigger related repository's
- Trigger invocation creation database function
- Project dependency's

### Removed
- The ability of the Network API to create trigger invocations
- Unused code

## [1.4.3] - 01-03-2021
### Added
- Storage histogram
- Router command statistics/monitoring
- Trigger execution histogram
- Management SQL functions

### Updated
- Improved router configuration
- Git ignore definition
- Live data service dependency's
- Development configurations
- Network API request auditting

### Removed
- Unused code
- Unused configuration

## [1.4.2] - 26-02-2021
### Added
- Timestamp variable to trigger message's
- Data contexts using native Npgsql functionality

### Updated
- Retarget the authorization context to Npgsql
- Retarget the networking context to Npgsql
- Logging package references
- Hosting package references

### Removed
- Remove references to Entity Framework Core
- Unused code

## [1.4.1] - 22-02-2021
### Updated
- Package dependency's
- Live data service authorization logging
- Live data authorization flow

### Removed
- Live data sensor links
- Networking dabase integration in the live data service

## [1.4.0] - 16-02-2021
### Added
- Routing cache
- Internal queue metrics

### Updated
- Command subscription QoS levels (from 1 to 3)
- Load tests for new caches

### Removed
- Ingress projects
- API projects (except the network API)
- Common projects (caching)

## [1.3.0] - 06-02-2021
### Updated
- Measurement query result JSON deserialization
- MeasurementQueryResult model annotations

### Removed
- Unused configuration files
- Unused deployment configuration

## [1.2.2] - 05-02-2021
### Updated
- Hardcode swagger and/or open API scheme's

### Added
- Set HTTP as possible scheme
- Set HTTPS as possible scheme

## [1.2.1] - 05-02-2021
### Added
- HTTPS swagger support

### Updated
- Network API swagger
- Auth API swagger
- Data API swagger
- Auth API swagger
- Dashboard API swagger

## [1.2.0] - 05-02-2021
### Updated
- Encoding storage conversion
- Sensor creation flow: publish sensor keys on the MQTT broker

### Added
- Ingress router request metrics
- Egress router request metrics

## [1.1.2] - 26-01-2021
### Updated
- Script directory name
- MQTT service project files
- CI/CD pipelines

### Added
- Security policy

### Removed
- CAKE build system
- Unused API code

## [1.1.1] - 26-01-2021
### Updated
- Update the versioning schema:
  - Update version of the MQTT service
  - Update the version of the core API's

## [1.1.0] - 26-01-2021
### Added
- Network:
  - Network project setup
  - Contracts project
  - Solution folders for:
    - Database project
    - General files
  - Message router
  - Network API + Gateway
  - Networking database:
    - Trigger administration
    - Sensor link administration
  - Live data service

- Platform:
  - Moved MQTT ingress service
  - Forward MQTT ingress to the HTTP gateway
  
- API:
  - Refactor SensateService into SensateIoT.API
  - Upgrade API's to .NET 5
  - Improve swagger documentation

### Removed
- Trigger administration (moved to network project)
- Network API (moved to network project)
- Trigger service (moved to network project)
- Live data service (moved to network project)

## [1.0.0] - 09-10-2020
### Added
- API's:
  - Network API
  - Data API
  - Authorization API
  - Blob API
  - Dashboard API
  
- Ingress services
- Data processing
