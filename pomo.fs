FeatureScript 3095;
import(path : "onshape/std/common.fs", version : "3095.0");

// Rebuild of Part Studio 1 (POMO): six flat parts laid out on the Front plane (XZ) and extruded toward -Y.
//   Frame         - 15" x 16" outline with four edge notches and an 11" x 14" window.
//   Back panel    - same outline and edge notches, no window.
//   Large ring    - half ring, 5.5"-7.5" radius, nine notches every 15 degrees.
//   Middle ring   - half ring, 3.375"-5.375" radius, five notches every 30 degrees.
//   Small ring    - half ring, 1.25"-3.25" radius, no notches.
//   Straight rail - 2" x 14.5" bar that slides into the ring notches.
// Every part's thickness is selectable. The ring notches are sized from the straight rail's thickness so the
// rail always fits them.

// Stock thicknesses offered for the parts. Add new options here and in THICKNESS_VALUES.
export enum PlateThickness
{
    annotation { "Name" : "1/2\"" }
    HALF_INCH,
    annotation { "Name" : "5/8\"" }
    FIVE_EIGHTHS_INCH,
    annotation { "Name" : "3/4\"" }
    THREE_QUARTERS_INCH
}

const THICKNESS_VALUES = {
        PlateThickness.HALF_INCH : 0.5 * inch,
        PlateThickness.FIVE_EIGHTHS_INCH : 0.625 * inch,
        PlateThickness.THREE_QUARTERS_INCH : 0.75 * inch
    };

// Extra notch width beyond the rail thickness: negative for a press fit, positive for a loose fit.
const FIT_BOUNDS = { (inch) : [-0.1, -0.002, 0.1] } as LengthBoundSpec;

// Frame and back panel outline.
const PANEL_WIDTH = 15 * inch;
const PANEL_HEIGHT = 16 * inch;
const PANEL_CORNER_RADIUS = 0.075 * inch;
const EDGE_NOTCH_INNER_X = 4.041 * inch;
const EDGE_NOTCH_OUTER_X = 4.707 * inch;
const EDGE_NOTCH_DEPTH = 0.472 * inch;
const EDGE_NOTCH_FILLET_RADIUS = 0.05 * inch;

// Frame window, centered on the panel.
const WINDOW_WIDTH = 11 * inch;
const WINDOW_HEIGHT = 14 * inch;
const WINDOW_CORNER_RADIUS = 0.75 * inch;

// Half rings, centered on the origin and spanning 0 to 180 degrees. Ring notches run radially in from the
// outer edge; NOTCH_DEPTH is measured from the outer radius to the notch's bottom corners.
const NOTCH_DEPTH = 1 * inch;
const LARGE_RING_RADII = [5.5 * inch, 7.5 * inch];
const LARGE_RING_NOTCH_ANGLES = [30, 45, 60, 75, 90, 105, 120, 135, 150];
const MIDDLE_RING_RADII = [3.375 * inch, 5.375 * inch];
const MIDDLE_RING_NOTCH_ANGLES = [30, 60, 90, 120, 150];
const SMALL_RING_RADII = [1.25 * inch, 3.25 * inch];

// Straight rail.
const STRAIGHT_RAIL_MIN = vector(-9.9019, -15.5) * inch;
const STRAIGHT_RAIL_MAX = vector(-7.9019, -1) * inch;

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

// Panel outline with its top edge on the origin: a rounded rectangle with a notch near each end of the top and
// bottom edges, the notch mouths filleted. Drawn one quadrant at a time in (x, depth-from-edge) coordinates.
function panelOutline(sketch is Sketch, prefix is string)
{
    const hw = PANEL_WIDTH / 2;
    const rc = PANEL_CORNER_RADIUS;
    const rf = EDGE_NOTCH_FILLET_RADIUS;
    const x0 = EDGE_NOTCH_INNER_X;
    const x1 = EDGE_NOTCH_OUTER_X;
    const d = EDGE_NOTCH_DEPTH;
    const zero = 0 * meter;
    const diag = 1 / sqrt(2);
    // Each entry is [start, end] for a line or [start, mid, end] for an arc.
    const quadrant = [
            [vector(zero, zero), vector(x0 - rf, zero)],
            [vector(x0 - rf, zero), vector(x0 - rf + rf * diag, rf - rf * diag), vector(x0, rf)],
            [vector(x0, rf), vector(x0, d)],
            [vector(x0, d), vector(x1, d)],
            [vector(x1, d), vector(x1, rf)],
            [vector(x1, rf), vector(x1 + rf - rf * diag, rf - rf * diag), vector(x1 + rf, zero)],
            [vector(x1 + rf, zero), vector(hw - rc, zero)],
            [vector(hw - rc, zero), vector(hw - rc + rc * diag, rc - rc * diag), vector(hw, rc)],
            [vector(hw, rc), vector(hw, PANEL_HEIGHT / 2)]
        ];
    var q = 0;
    for (var sx in [1, -1])
    {
        for (var fromBottom in [false, true])
        {
            const toSketch = function(p) { return vector(sx * p[0], fromBottom ? p[1] - PANEL_HEIGHT : -p[1]); };
            for (var k = 0; k < size(quadrant); k += 1)
            {
                const seg = quadrant[k];
                const segId = prefix ~ q ~ "_" ~ k;
                if (size(seg) == 2)
                    skLineSegment(sketch, segId, { "start" : toSketch(seg[0]), "end" : toSketch(seg[1]) });
                else
                    skArc(sketch, segId, { "start" : toSketch(seg[0]), "mid" : toSketch(seg[1]), "end" : toSketch(seg[2]) });
            }
            q += 1;
        }
    }
}

// Half ring between two radii, spanning 0 to 180 degrees.
function halfRing(sketch is Sketch, prefix is string, radii is array)
{
    const ri = radii[0];
    const ro = radii[1];
    const zero = 0 * meter;
    skLineSegment(sketch, prefix ~ "right", { "start" : vector(ri, zero), "end" : vector(ro, zero) });
    skArc(sketch, prefix ~ "outer", { "start" : vector(ro, zero), "mid" : vector(zero, ro), "end" : vector(-ro, zero) });
    skLineSegment(sketch, prefix ~ "left", { "start" : vector(-ro, zero), "end" : vector(-ri, zero) });
    skArc(sketch, prefix ~ "inner", { "start" : vector(-ri, zero), "mid" : vector(zero, ri), "end" : vector(ri, zero) });
}

// Rectangular notches cut radially into a ring's outer edge; each notch's bottom corners sit NOTCH_DEPTH
// inside the outer radius.
function notchCutters(sketch is Sketch, prefix is string, outerRadius is ValueWithUnits, width is ValueWithUnits, anglesInDegrees is array)
{
    const hw = width / 2;
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
        annotation { "Name" : "Frame thickness" }
        definition.frameThickness is PlateThickness;
        annotation { "Name" : "Back panel thickness" }
        definition.panelThickness is PlateThickness;
        annotation { "Name" : "Ring thickness" }
        definition.ringThickness is PlateThickness;
        annotation { "Name" : "Straight rail thickness" }
        definition.railThickness is PlateThickness;
        annotation { "Name" : "Notch fit allowance", "Description" : "Added to the rail thickness to size the ring notches. Negative is tighter." }
        isLength(definition.notchFit, FIT_BOUNDS);
    }
    {
        const sketchPlane = plane(vector(0, 0, 0) * meter, vector(0, -1, 0), vector(1, 0, 0));
        const notchWidth = THICKNESS_VALUES[definition.railThickness] + definition.notchFit;
        if (notchWidth <= 0 * inch)
            throw regenError("Notch fit allowance leaves no notch width.", ["notchFit"]);

        const extrude = function(name is string, thickness is PlateThickness, drawing is function) returns Query
            {
                const sketchId = id + (name ~ "Sketch");
                const sketch = newSketchOnPlane(context, sketchId, { "sketchPlane" : sketchPlane });
                drawing(sketch);
                skSolve(sketch);
                opExtrude(context, id + name, {
                            "entities" : qSketchRegion(sketchId, true),
                            "direction" : sketchPlane.normal,
                            "endBound" : BoundingType.BLIND,
                            "endDepth" : THICKNESS_VALUES[thickness]
                        });
                opDeleteBodies(context, id + (name ~ "DeleteSketch"), { "entities" : qCreatedBy(sketchId, EntityType.BODY) });
                return qCreatedBy(id + name, EntityType.BODY);
            };
        const nameParts = function(part is Query, partName is string)
            {
                setProperty(context, { "entities" : part, "propertyType" : PropertyType.NAME, "value" : partName });
            };
        const notchedRing = function(name is string, partName is string, radii is array, notchAngles is array)
            {
                const ring = extrude(name, definition.ringThickness, function(sketch) { halfRing(sketch, "ring", radii); });
                if (notchAngles != [])
                {
                    const cutters = extrude(name ~ "Notches", definition.ringThickness, function(sketch)
                        {
                            notchCutters(sketch, "notch", radii[1], notchWidth, notchAngles);
                        });
                    opBoolean(context, id + (name ~ "Cut"), { "tools" : cutters, "targets" : ring, "operationType" : BooleanOperationType.SUBTRACTION });
                }
                nameParts(ring, partName);
            };

        nameParts(extrude("frame", definition.frameThickness, function(sketch)
                {
                    panelOutline(sketch, "outline");
                    roundedRectangle(sketch, "window", 0 * meter, -PANEL_HEIGHT / 2, WINDOW_WIDTH, WINDOW_HEIGHT, WINDOW_CORNER_RADIUS);
                }), "Frame");
        nameParts(extrude("panel", definition.panelThickness, function(sketch) { panelOutline(sketch, "outline"); }), "Back panel");
        notchedRing("largeRing", "Large ring", LARGE_RING_RADII, LARGE_RING_NOTCH_ANGLES);
        notchedRing("middleRing", "Middle ring", MIDDLE_RING_RADII, MIDDLE_RING_NOTCH_ANGLES);
        notchedRing("smallRing", "Small ring", SMALL_RING_RADII, []);
        nameParts(extrude("straightRail", definition.railThickness, function(sketch)
                {
                    skRectangle(sketch, "bar", { "firstCorner" : STRAIGHT_RAIL_MIN, "secondCorner" : STRAIGHT_RAIL_MAX });
                }), "Straight rail");
    }, {
        frameThickness : PlateThickness.THREE_QUARTERS_INCH,
        panelThickness : PlateThickness.HALF_INCH,
        ringThickness : PlateThickness.HALF_INCH,
        railThickness : PlateThickness.HALF_INCH,
        notchFit : -0.002 * inch
    });
