FeatureScript 3095;
import(path : "onshape/std/common.fs", version : "3095.0");

// Rebuild of POMO.step ("Part Studio 1" / "Part 1"): a 15" x 16" plate (1/2", 5/8" or 3/4" thick) with
// 0.075" corner radii and an 11" x 1" through-slot (0.125" corner radii) centered 8" below the top edge.
// Placement matches the original export: profile on the Front plane (XZ), top edge on the origin,
// plate hanging toward -Z and extruded toward -Y.

// Draws a closed rounded rectangle centered at (cx, cy) in sketch coordinates.
function roundedRectangle(sketch is Sketch, prefix is string, cx is ValueWithUnits, cy is ValueWithUnits,
    w is ValueWithUnits, h is ValueWithUnits, r is ValueWithUnits)
{
    const hw = w / 2;
    const hh = h / 2;
    skLineSegment(sketch, prefix ~ "bottom", { "start" : vector(cx - hw + r, cy - hh), "end" : vector(cx + hw - r, cy - hh) });
    skLineSegment(sketch, prefix ~ "right", { "start" : vector(cx + hw, cy - hh + r), "end" : vector(cx + hw, cy + hh - r) });
    skLineSegment(sketch, prefix ~ "top", { "start" : vector(cx + hw - r, cy + hh), "end" : vector(cx - hw + r, cy + hh) });
    skLineSegment(sketch, prefix ~ "left", { "start" : vector(cx - hw, cy + hh - r), "end" : vector(cx - hw, cy - hh + r) });
    var i = 0;
    for (var s in [[1, -1], [1, 1], [-1, 1], [-1, -1]])
    {
        const c = vector(cx + s[0] * (hw - r), cy + s[1] * (hh - r));
        skArc(sketch, prefix ~ "arc" ~ i, {
                    "start" : c + vector(s[0] * r, 0 * meter),
                    "mid" : c + vector(s[0] * r, s[1] * r) / sqrt(2),
                    "end" : c + vector(0 * meter, s[1] * r)
                });
        i += 1;
    }
}

// Stock thicknesses offered for the plate. Add new options here and in PLATE_THICKNESS_VALUES.
export enum PlateThickness
{
    annotation { "Name" : "1/2\"" }
    HALF_INCH,
    annotation { "Name" : "5/8\"" }
    FIVE_EIGHTHS_INCH,
    annotation { "Name" : "3/4\"" }
    THREE_QUARTERS_INCH
}

const PLATE_THICKNESS_VALUES = {
        PlateThickness.HALF_INCH : 0.5 * inch,
        PlateThickness.FIVE_EIGHTHS_INCH : 0.625 * inch,
        PlateThickness.THREE_QUARTERS_INCH : 0.75 * inch
    };

annotation { "Feature Type Name" : "POMO Plate" }
export const pomoPlate = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Width" }
        isLength(definition.width, LENGTH_BOUNDS);
        annotation { "Name" : "Height" }
        isLength(definition.height, LENGTH_BOUNDS);
        annotation { "Name" : "Thickness" }
        definition.thickness is PlateThickness;
        annotation { "Name" : "Corner radius" }
        isLength(definition.cornerRadius, LENGTH_BOUNDS);
        annotation { "Name" : "Slot width" }
        isLength(definition.slotWidth, LENGTH_BOUNDS);
        annotation { "Name" : "Slot height" }
        isLength(definition.slotHeight, LENGTH_BOUNDS);
        annotation { "Name" : "Slot corner radius" }
        isLength(definition.slotCornerRadius, LENGTH_BOUNDS);
        annotation { "Name" : "Slot center from top edge" }
        isLength(definition.slotOffset, LENGTH_BOUNDS);
    }
    {
        const sketchPlane = plane(vector(0, 0, 0) * meter, vector(0, -1, 0), vector(1, 0, 0));
        const sketch = newSketchOnPlane(context, id + "sketch", { "sketchPlane" : sketchPlane });
        roundedRectangle(sketch, "outer", 0 * meter, -definition.height / 2, definition.width, definition.height, definition.cornerRadius);
        roundedRectangle(sketch, "slot", 0 * meter, -definition.slotOffset, definition.slotWidth, definition.slotHeight, definition.slotCornerRadius);
        skSolve(sketch);

        opExtrude(context, id + "extrude", {
                    "entities" : qSketchRegion(id + "sketch", true),
                    "direction" : sketchPlane.normal,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : PLATE_THICKNESS_VALUES[definition.thickness]
                });
        opDeleteBodies(context, id + "deleteSketch", { "entities" : qCreatedBy(id + "sketch", EntityType.BODY) });
    }, {
        width : 15 * inch,
        height : 16 * inch,
        thickness : PlateThickness.THREE_QUARTERS_INCH,
        cornerRadius : 0.075 * inch,
        slotWidth : 11 * inch,
        slotHeight : 1 * inch,
        slotCornerRadius : 0.125 * inch,
        slotOffset : 8 * inch
    });
