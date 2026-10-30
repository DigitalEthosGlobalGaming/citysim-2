# Suburban and roads model imports

Kenney City Kit Suburban 2.0 and City Kit Roads 2.2, CC0; source licenses and FBX/atlas copies are in `Assets/raw/kenney/suburban` and `Assets/raw/kenney/roads`. Individual JSON manifests record imports. Native ModelDoc files use the corresponding `materials/kenney/suburban.vmat` or `roads.vmat` atlas material. Models are static visual meshes, without authored collisions.

## Mappings

- House: `models/kenney/suburban/building-type-a.vmdl` (existing import); cottage: `building-type-g`; vineyard or Gardener's Guild building component: `building-type-i`.
- Orchard, park, sapling and forest: `tree-large`, `tree-small`, `planter`, and `path-stones-long`, `path-stones-messy`, `path-stones-short` are components to arrange. Fountain still needs a fountain; orchard needs fruit, vineyard needs vines. These components do not represent complete tiles by themselves.
- Street: `models/kenney/roads/road-end`, `road-straight`, `road-bend`, `road-intersection`, `road-crossroad`. `road-crossroad-line` provides an optional marked intersection. Avenue still needs a distinctive treatment. `road-roundabout` is a dedicated roundabout, but has a different footprint and lane scale.

All paths above end in `.vmdl` and suburban component names share `models/kenney/suburban/`. None of these are bespoke landmarks.

## Geometry and placement

Bounds at native import scale 1, in s&box units, measured from loaded compiled models:

| Model | X × Y × Z | Minimum Z |
| --- | --- | --- |
| Road end, straight, bend, intersection, crossroad, crossroad-line | 100 × 100 × 2 | 0 |
| Road curve | 200 × 200 × 2 | 0 |
| Road roundabout | 300 × 300 × 2 | 0 |
| Cottage g | 117.8 × 145 × 76.82 | 0 |
| Building i | 102.8 × 128.64 × 73.75 | 0 |
| Large / small tree | 24.3 × 21.04 × 76.7 / 56.7 | 0 |
| Planter | 30 × 40 × 17.71 | 0 |
| Long / messy / short stone path | 40 × 14 × 1 / 36.14 × 15.56 × 1 / 20 × 14 × 1 | 0 |

For a 50-unit square grid, use scale 0.5 for the 100-unit road pieces, with the pivot at cell centre and Z at the board surface; they rise one unit above that surface. The 200-unit curve is not a one-cell bend: use `road-bend`. The 300-unit roundabout at scale 0.5 spans 3 × 3 cells; reducing to one cell also reduces lane width to one third, so it does not seamlessly connect to regular pieces. Road and building axes/yaw offsets have not been verified in an oriented game fixture; rotate and verify each connectivity mask in the game before relying on it. Props start at Z=0; fit their scale to the target tile and place their origin at ground height rather than centering their bounding box vertically. No in-game visual integration was tested here.

## Verification and Lake

Sixteen new imports compiled successfully, loaded with `Model.IsError == false`, positive bounds, and their intended material. Editor thumbnails of bend, intersection, roundabout, cottage and large tree show expected colours and geometry. ModelDoc emits the benign single-LOD-group warning. Road-bend dependency inspection resolves its material.

Existing `models/roads/street_water.vmdl` contains multiple body-group choices including `15_water_empty`, with a water material remap, but its default thumbnail shows a road. Rendering the existing model also logged missing `white.011.vmat_c`. Its water choice and suitability for a lake have not been visually verified; it should not be assumed to be a ready lake tile. A dedicated water asset or a tested body-group and material repair remains necessary.
