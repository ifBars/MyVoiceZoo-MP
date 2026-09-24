# Male character artwork

Custom blond character with the cheek bandage retained, designed against the game's local visual references. The original reference photograph and extracted game sprites are not included here.

- `male-blond-concept.png`: character and costume design reference.
- `sheets/0.png` through `sheets/4.png`: generated source animation sheets for Default, Duck, Reindeer, Frog and Cat.
- `male/`: 45 normalized transparent runtime frames, embedded in the mod DLL.

Each outfit follows the native three Idle labels and six Run labels. The mod creates separate sprite libraries and reuses native animation timing, pivots and sorting. Native Lucy libraries are never edited.

`scripts/Prepare-CharacterSprites.ps1` splits the generated sheets and uses a local CharacterProbe export only for dimensions and foot baselines. No pixels from the native reference textures are copied into the output. Keep that export outside Git.
