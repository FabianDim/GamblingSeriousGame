# Archived pet painting prototype

Archived on 2026-09-25 when companion setup switched to preset animals and breeds.
This directory is outside Assets so Unity does not import or compile these files.

Preserved: painting controller, texture/undo/export engine (and its original metadata),
UXML, stylesheet, original documentation, and isolated validation utility.
The old validation utility references the historical BackToPetSelection navigation method.

The live PetCustomizationScreen keeps its original script GUID and prefab references,
but now uses sprite previews, breed selection and names. To restore the prototype,
restore the archived controller, UXML and stylesheet to their original paths and move
PetPaintTexture.cs plus its metadata into Assets/Scripts/UI. Do not import both
controllers at once. The archive documentation describes the old APIs, not the live screen.
