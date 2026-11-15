# Maestro authoring descriptor v1

Put `maestro.json` next to the provider's FCS file. The top-level fields are `schemaVersion: 1`, `provider` (stable ID), `name` and `actions`. Action IDs are unique within a provider. Each action supplies an ID, label, target `recordType` (case-sensitive Meitou FcsRecordType name), ordered `inputs` and ordered `edits`. A bad descriptor is skipped with its filename and reason. An installed provider can be chosen before enabling it for play. Dependencies referenced by gameplay must be enabled when testing the export.

An input has a key, label, kind, optional default and optional flag. Supported kinds are **text**, **selection** (the selected FCS IDs), **boolean**, **number**, **vector** (three finite values), **bone** (a bone common to the selected animals), and **asset** (a source file or existing pack-relative path). The optional preview binds selection, anchor, mesh, offset, rotation, seat and facing to input keys. It displays the animal bind pose with +Y up, +Z forward, offsets in model units and angles in degrees. Rotation uses yaw/pitch/roll in Y/X/Z order, matching the runtime. Seat coordinates are local to the saddle.

| Edit | Required bindings and result |
| --- | --- |
| `dependency` | `file` names the installed FCS dependency. Add it once to the writable project header. References to inherited records also add the defining mod where appropriate. |
| `asset` | `input`, `directory`, and allowed `extensions`. Import under the given directory with the source basename, then bind the input to the resulting relative path. Optional `requires` ties a texture to its mesh. |
| `jsonEntry` | `file`, `versionField`, `version`, `preserveVersions`, `ownerField` and `owner` describe the container. `array`, `idField`, `id` and `fields` map a stable entry to input values. Optional `ownerPrefix` requires an owner-prefixed ID. `uniqueSelection` prevents two entries claiming one selected record within the pack. `upgradeDrop` removes a previously ignored field from a named array on upgrade from the indicated version. Other entries and fields are retained. |
| `reference` | `list` is the FCS reference category, `target` the provider's stable record ID and `value` a boolean input bound to reference value 0. Reapplying updates the same target. An inherited record receives a project-local override. |

The Gear Conditions example uses condition code 18432 and a DIALOG_ACTION whose subject is the conversation target. Its boolean value distinguishes equipped from unequipped. The checks read the subject's head slot. The descriptor writes references to that action; the interpreter contains no dialogue-specific branch or provider ID.
