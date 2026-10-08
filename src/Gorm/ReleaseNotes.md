# Change history
## 3.1.0:
- Added the Temporal Digital Twin & Simulation Engine with bitemporal worlds, deterministic diffs and timelines.
## 3.0.3:
- Pipeline trigger added
## 3.0.2:
- Add method to load outgoing edges by source nodes.
## 3.0.1:
- Fix SQ issue and failing test on appsettings.
## 3.0.0:
- Added Graph Intelligence Engine (G)IE
## 2.11.0:
- Add support for DateOnly/TimeOnly and enhance SQL queries
## 2.10.1:
- Fix GraphMaterializer reads columns based on property/materialization order, not guaranteed SQL column order.
## 2.10.0:
- Add graph configuration interfaces and extensions.
## 2.9.0:
- Refactor property mapping and validation logic.
## 2.8.0:
- Optimize query execution and relationship handling.
## 2.7.0:
- Security hardening: escape generated SQL identifiers, LIKE wildcard patterns, savepoint/history table names, and prevent arbitrary captured method execution during query translation.
## 2.6.0:
- Removed DbUp mutations, added schema drift validation, SQL hardening, OpenTelemetry diagnostics, lookup helpers and docs.
## 2.5.0:
- Add mutation factory.
## 2.4.0:
- Added Renovate.
## 2.3.0:
- Add batch graph mutation API for atomic node/edge saves.
## 2.2.1:
- Remove query/SQL caching and add test for predicate capture. 
## 2.2.0:
- Added small extension fixes to support better integration with graph databases.
- Added comprehensive unit tests for Gorm core APIs and queries
## 2.1.0:
- Added support for historical data persistence in graph database.
## 2.0.0:
- Altered interface to support in-memory graph and graph databases.
## 1.0.1:
- Bugfix empty Ids when loading nodes and edges.
## 1.0.0:
- Initial release.