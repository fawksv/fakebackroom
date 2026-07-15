# PlayerPrototype

This folder contains an isolated first-person player prototype. It does not require tags, layers, project settings changes, input actions, existing prefabs, or existing project scripts.

Manual setup steps:

1. In any test scene, create an empty GameObject named `PlayerPrototype`.
2. Add a `CharacterController` component to the root GameObject.
3. Add `FirstPersonPrototypeController` to the root GameObject.
4. Create a `Camera` child object under the root GameObject.
5. Drag the `Camera` child object into the controller's view reference slot.
6. Recommended `CharacterController` values:
   - Height = 1.8
   - Radius = 0.35
   - Center = (0, 0.9, 0)
   - Step Offset = 0.3
7. Recommended local position for the `Camera` child:
   - (0, 1.6, 0)
8. The scene floor needs a `Collider`.
9. No `Player` tag is required.
10. No Project Settings changes are required.

Optional interaction setup:

1. Add `PlayerPrototypeInteractor` to the `PlayerPrototype` root GameObject.
2. Drag the same `Camera` child object into the interactor's view reference slot.
3. The interactor draws a simple crosshair and prompt with built-in `OnGUI`, so no Canvas or UI package is required.
4. Assign `Assets/PlayerPrototype/Fonts/FZYTK.TTF` to the interactor's prompt font field if you want to use the prototype font.
5. To make a test door, create a Cube with a Collider and add `PrototypeDoor`.
6. To make a test pickup, create a small Cube or Sphere with a Collider and add `PrototypePickup`.
7. Aim the center of the screen at the object and press `E` to interact.

Door note:

- `PrototypeDoor` rotates around the object's pivot. For a better hinged door, put the visible door mesh under an empty parent placed at the hinge, then add `PrototypeDoor` to the parent.
