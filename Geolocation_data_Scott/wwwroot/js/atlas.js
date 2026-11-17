// Minimal pan/zoom bridge for the SVG atlas. Blazor owns the viewport state;
// this module only reports gestures in map (viewBox) coordinates.
export function attach(svg, dotNetRef) {
    let dragging = false;
    let moved = false;
    let last = null;

    const toLocal = (event) => {
        const point = svg.createSVGPoint();
        point.x = event.clientX;
        point.y = event.clientY;
        return point.matrixTransform(svg.getScreenCTM().inverse());
    };

    const onWheel = (event) => {
        event.preventDefault();
        const point = toLocal(event);
        const factor = event.deltaY < 0 ? 1.18 : 1 / 1.18;
        dotNetRef.invokeMethodAsync("ZoomAt", factor, point.x, point.y);
    };

    const onPointerDown = (event) => {
        if (event.button !== 0) {
            return;
        }

        dragging = true;
        moved = false;
        last = toLocal(event);
    };

    const onPointerMove = (event) => {
        if (!dragging) {
            return;
        }

        const point = toLocal(event);
        const dx = point.x - last.x;
        const dy = point.y - last.y;
        last = point;

        if (Math.abs(dx) + Math.abs(dy) > 0.4) {
            moved = true;
            dotNetRef.invokeMethodAsync("PanBy", dx, dy);
        }
    };

    const onPointerUp = () => {
        dragging = false;
    };

    // Suppress the click that follows a drag so it does not clear the selection.
    const onClickCapture = (event) => {
        if (moved) {
            event.stopPropagation();
            event.preventDefault();
            moved = false;
        }
    };

    svg.addEventListener("wheel", onWheel, { passive: false });
    svg.addEventListener("pointerdown", onPointerDown);
    svg.addEventListener("click", onClickCapture, true);
    window.addEventListener("pointermove", onPointerMove);
    window.addEventListener("pointerup", onPointerUp);
    window.addEventListener("pointercancel", onPointerUp);

    return {
        dispose() {
            svg.removeEventListener("wheel", onWheel);
            svg.removeEventListener("pointerdown", onPointerDown);
            svg.removeEventListener("click", onClickCapture, true);
            window.removeEventListener("pointermove", onPointerMove);
            window.removeEventListener("pointerup", onPointerUp);
            window.removeEventListener("pointercancel", onPointerUp);
        }
    };
}
