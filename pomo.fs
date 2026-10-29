FeatureScript 3095;
import(path : "onshape/std/common.fs", version : "3095.0");

// Rebuild of Part Studio 1 (POMO): three flat parts cut from 1/2", 5/8" or 3/4" stock.
//   Plate       - 15" x 16" plate (0.075" corner radii) with an 11" x 1" through-slot (0.125" corner radii)
//                 centered 8" below its top edge, joined to a half-ring rail (5.5"-7.5" radius) on its top
//                 edge with nine notches every 15 degrees.
//   Outer rail  - ring segment (12"-14" radius) with 2" feet at each end and eight notches every 7 degrees.
//   Straight rail - 2" x 14.5" bar beside the plate.
// Placement matches the original export: profiles on the Front plane (XZ) with the plate's top edge and
// the ring centers on the origin, extruded toward -Y.

// Stock thicknesses offered for the parts. Add new options here and in PLATE_THICKNESS_VALUES.
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

// Notches cut into the outer edge of both curved rails.
const NOTCH_WIDTH = 0.498 * inch;
const NOTCH_DEPTH = 1 * inch; // from the outer radius to the notch's bottom corners

// Half-ring rail on top of the plate.
const INNER_RAIL_INNER_RADIUS = 5.5 * inch;
const INNER_RAIL_OUTER_RADIUS = 7.5 * inch;
const INNER_RAIL_NOTCH_ANGLES = [30, 45, 60, 75, 90, 105, 120, 135, 150];

// Outer ring rail with feet.
const OUTER_RAIL_INNER_RADIUS = 12 * inch;
const OUTER_RAIL_OUTER_RADIUS = 14 * inch;
const OUTER_RAIL_FOOT_OUTER_X = 7.5 * inch;
const OUTER_RAIL_FOOT_INNER_X = 5.48 * inch;
const OUTER_RAIL_FOOT_BOTTOM = 9.82 * inch;
const OUTER_RAIL_FILLET_RADIUS = 0.5 * inch;
const OUTER_RAIL_NOTCH_ANGLES = [65.5, 72.5, 79.5, 86.5, 93.5, 100.5, 107.5, 114.5];

// Straight rail.
const STRAIGHT_RAIL_MIN = vector(-11.881, -15.25) * inch;
const STRAIGHT_RAIL_MAX = vector(-9.881, -0.75) * inch;

function polar(radius is ValueWithUnits, angle is ValueWithUnits) returns Vector
{
    return vector(radius * cos(angle), radius * sin(angle));
}

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

// Half ring between two radii, spanning 0 to 180 degrees.
function halfRing(sketch is Sketch, prefix is string, innerRadius is ValueWithUnits, outerRadius is ValueWithUnits)
{
    skLineSegment(sketch, prefix ~ "right", { "start" : vector(innerRadius, 0 * meter), "end" : vector(outerRadius, 0 * meter) });
    skArc(sketch, prefix ~ "outer", { "start" : vector(outerRadius, 0 * meter), "mid" : vector(0 * meter, outerRadius), "end" : vector(-outerRadius, 0 * meter) });
    skLineSegment(sketch, prefix ~ "left", { "start" : vector(-outerRadius, 0 * meter), "end" : vector(-innerRadius, 0 * meter) });
    skArc(sketch, prefix ~ "inner", { "start" : vector(-innerRadius, 0 * meter), "mid" : vector(0 * meter, innerRadius), "end" : vector(innerRadius, 0 * meter) });
}

// Ring segment between two radii whose ends drop into rectangular feet, with a fillet where the outer arc
// meets each foot. Symmetric about the vertical axis.
function footedRing(sketch is Sketch, prefix is string)
{
    const ri = OUTER_RAIL_INNER_RADIUS;
    const ro = OUTER_RAIL_OUTER_RADIUS;
    const xo = OUTER_RAIL_FOOT_OUTER_X;
    const xi = OUTER_RAIL_FOOT_INNER_X;
    const fr = OUTER_RAIL_FILLET_RADIUS;
    const filletCenter = vector(xo - fr, sqrt((ro - fr) ^ 2 - (xo - fr) ^ 2));
    const arcTangent = filletCenter * (ro / norm(filletCenter));
    const footTangent = vector(xo, filletCenter[1]);
    const innerEnd = vector(xi, sqrt(ri ^ 2 - xi ^ 2));
    const filletMid = filletCenter + normalize((arcTangent - filletCenter) + (footTangent - filletCenter)) * fr;
    for (var side in [1, -1])
    {
        const m = function(v) { return vector(side * v[0], v[1]); };
        const suffix = side > 0 ? "R" : "L";
        skArc(sketch, prefix ~ "fillet" ~ suffix, { "start" : m(arcTangent), "mid" : m(filletMid), "end" : m(footTangent) });
        skLineSegment(sketch, prefix ~ "footOuter" ~ suffix, { "start" : m(footTangent), "end" : m(vector(xo, OUTER_RAIL_FOOT_BOTTOM)) });
        skLineSegment(sketch, prefix ~ "footBottom" ~ suffix, { "start" : m(vector(xo, OUTER_RAIL_FOOT_BOTTOM)), "end" : m(vector(xi, OUTER_RAIL_FOOT_BOTTOM)) });
        skLineSegment(sketch, prefix ~ "footInner" ~ suffix, { "start" : m(vector(xi, OUTER_RAIL_FOOT_BOTTOM)), "end" : m(innerEnd) });
    }
    skArc(sketch, prefix ~ "outer", { "start" : arcTangent, "mid" : vector(0 * meter, ro), "end" : vector(-arcTangent[0], arcTangent[1]) });
    skArc(sketch, prefix ~ "inner", { "start" : innerEnd, "mid" : vector(0 * meter, ri), "end" : vector(-innerEnd[0], innerEnd[1]) });
}

// Rectangular notches cut radially into a ring's outer edge; each notch's bottom corners sit
// NOTCH_DEPTH inside the outer radius.
function notchCutters(sketch is Sketch, prefix is string, outerRadius is ValueWithUnits, anglesInDegrees is array)
{
    const hw = NOTCH_WIDTH / 2;
    const bottom = sqrt((outerRadius - NOTCH_DEPTH) ^ 2 - hw ^ 2);
    const top = outerRadius + 0.5 * inch;
    var i = 0;
    for (var a in anglesInDegrees)
    {
        const u = polar(1 * meter, a * degree) / meter;
        const p = vector(-u[1], u[0]);
        const corners = [u * bottom - p * hw, u * bottom + p * hw, u * top + p * hw, u * top - p * hw];
        for (var k = 0; k < 4; k += 1)
        {
            skLineSegment(sketch, prefix ~ i ~ "_" ~ k, { "start" : corners[k], "end" : corners[(k + 1) % 4] });
        }
        i += 1;
    }
}

annotation { "Feature Type Name" : "POMO Parts" }
export const pomoParts = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Thickness" }
        definition.thickness is PlateThickness;
        annotation { "Name" : "Width" }
        isLength(definition.width, LENGTH_BOUNDS);
        annotation { "Name" : "Height" }
        isLength(definition.height, LENGTH_BOUNDS);
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
        const thickness = PLATE_THICKNESS_VALUES[definition.thickness];
        const sketchPlane = plane(vector(0, 0, 0) * meter, vector(0, -1, 0), vector(1, 0, 0));
        const extrudeSketch = function(name is string, drawing is function)
            {
                const sketch = newSketchOnPlane(context, id + (name ~ "Sketch"), { "sketchPlane" : sketchPlane });
                drawing(sketch);
                skSolve(sketch);
                opExtrude(context, id + name, {
                            "entities" : qSketchRegion(id + (name ~ "Sketch"), true),
                            "direction" : sketchPlane.normal,
                            "endBound" : BoundingType.BLIND,
                            "endDepth" : thickness
                        });
                opDeleteBodies(context, id + (name ~ "DeleteSketch"), { "entities" : qCreatedBy(id + (name ~ "Sketch"), EntityType.BODY) });
                return qCreatedBy(id + name, EntityType.BODY);
            };

        const plate = extrudeSketch("plate", function(sketch)
            {
                roundedRectangle(sketch, "outer", 0 * meter, -definition.height / 2, definition.width, definition.height, definition.cornerRadius);
                roundedRectangle(sketch, "slot", 0 * meter, -definition.slotOffset, definition.slotWidth, definition.slotHeight, definition.slotCornerRadius);
            });
        const innerRail = extrudeSketch("innerRail", function(sketch)
            {
                halfRing(sketch, "ring", INNER_RAIL_INNER_RADIUS, INNER_RAIL_OUTER_RADIUS);
            });
        const innerNotches = extrudeSketch("innerNotches", function(sketch)
            {
                notchCutters(sketch, "notch", INNER_RAIL_OUTER_RADIUS, INNER_RAIL_NOTCH_ANGLES);
            });
        opBoolean(context, id + "cutInnerNotches", { "tools" : innerNotches, "targets" : innerRail, "operationType" : BooleanOperationType.SUBTRACTION });
        opBoolean(context, id + "joinInnerRail", { "tools" : qUnion([plate, innerRail]), "operationType" : BooleanOperationType.UNION });

        const outerRail = extrudeSketch("outerRail", function(sketch)
            {
                footedRing(sketch, "ring");
            });
        const outerNotches = extrudeSketch("outerNotches", function(sketch)
            {
                notchCutters(sketch, "notch", OUTER_RAIL_OUTER_RADIUS, OUTER_RAIL_NOTCH_ANGLES);
            });
        opBoolean(context, id + "cutOuterNotches", { "tools" : outerNotches, "targets" : outerRail, "operationType" : BooleanOperationType.SUBTRACTION });

        extrudeSketch("straightRail", function(sketch)
            {
                skRectangle(sketch, "bar", { "firstCorner" : STRAIGHT_RAIL_MIN, "secondCorner" : STRAIGHT_RAIL_MAX });
            });
    }, {
        thickness : PlateThickness.THREE_QUARTERS_INCH,
        width : 15 * inch,
        height : 16 * inch,
        cornerRadius : 0.075 * inch,
        slotWidth : 11 * inch,
        slotHeight : 1 * inch,
        slotCornerRadius : 0.125 * inch,
        slotOffset : 8 * inch
    });
