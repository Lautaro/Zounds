using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class EnvelopeGUI {

        private static readonly Color backgroundColor = new Color(98f / 255f, 105f / 255f, 138f / 255f);
        private static readonly Color waveformColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly int[] emptyIntArray = new int[0];

        public class EnvelopeGUIPair {
            public Envelope envelope;
            public EnvelopeGUI gui;
        }

        public string name;

        private int draggedPointIndex = -1;
        private Envelope.Point draggedPoint = null;

        private int draggedLineIndex = -1;
        private Envelope.Point draggedLine = null;

        private int draggedExponentIndex = -1;
        private Envelope.Point draggedExponent = null;

        private bool isBoxSelecting = false;
        private static EnvelopeGUI lastActiveGUI = null;
        private Vector2 startBoxPos;
        private List<int> selectedIndices = new List<int>();

        public void ResetStates() {
            draggedPointIndex = -1;
            draggedLineIndex = -1;
            draggedExponentIndex = -1;
            isBoxSelecting = false;
            lastActiveGUI = null;
            selectedIndices.Clear();
        }

        /// <summary>
        /// Draw fully stretched GUILayout.
        /// </summary>
        /// <param name="envelope"></param>
        public bool DrawLayout(Envelope envelope, Color mainColor, bool allowAddPointByDoubleClick = false) {
            return DrawLayout(envelope, 2, 2, mainColor, allowAddPointByDoubleClick, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }

        public bool DrawLayout(Envelope envelope, float width, float height, Color mainColor, params GUILayoutOption[] options) {
            var rect = GUILayoutUtility.GetRect(width, height, options);
            return Draw(rect, envelope, mainColor);
        }

        public bool DrawLayout(Envelope envelope, float width, float height, Color mainColor, bool allowAddPointByDoubleClick, params GUILayoutOption[] options) {
            var rect = GUILayoutUtility.GetRect(width, height, options);
            return Draw(rect, envelope, mainColor, allowAddPointByDoubleClick);
        }

        public bool Draw(Rect rect, Envelope envelope, Color mainColor, bool allowAddPointByDoubleClick = true) {
            bool dirty = DrawHandles(rect, envelope, mainColor, allowAddPointByDoubleClick);
            //DrawValueLabels(envelope, rect);
            return dirty;
        }

        #region DRAWERS

        private bool DrawHandles(Rect rect, Envelope envelope, Color mainColor, bool allowAddPointByDoubleClick) {
            bool dirty = false;
            float xRange = envelope.xMax - envelope.xMin;
            float yRange = envelope.yMax - envelope.yMin;
            Vector3 offset = rect.position;
            Vector3 size = rect.size;

            Handles.BeginGUI();
            var handlesColor = Handles.color;
            {
                DrawGrid(envelope, xRange, yRange, offset, size);
                DrawMainLine(envelope, xRange, yRange, offset, size, mainColor);
                DrawPoints(envelope, xRange, yRange, offset, size);
                if (HandleMouseInput(envelope, xRange, yRange, offset, size, allowAddPointByDoubleClick)) dirty = true;
                if (HandleKeyboardInput(envelope)) dirty = true;
            }
            Handles.color = handlesColor;
            Handles.EndGUI();
            return dirty;
        }

        private void DrawGrid(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size) {
            Handles.color = new Color(0.25f, 0.25f, 0.25f, 0.5f);

            float xStep = 0.02f;
            float yStep = 0.1f;

            for (float i = envelope.yMin % yStep; i < yRange; i += yStep) {
                float y = size.y - (i / yRange * size.y);
                Handles.DrawLine(new Vector3(0, y, 0) + offset, new Vector3(size.x, y, 0f) + offset);
            }

            for (float i = 0f; i < xRange; i += xStep) {
                float x = i / xRange * size.x;
                Handles.DrawLine(new Vector3(x, 0, 0) + offset, new Vector3(x, size.y, 0f) + offset);
            }

            //xStep = 0.1f;
            //for (float i = 0f; i < xRange; i += xStep) {
            //    float x = i / xRange * size.x;
            //    Handles.DrawLine(new Vector3(x, 0, 0) + offset, new Vector3(x, size.y, 0f) + offset);
            //}
        }

        private void DrawMainLine(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Color color) {
            Handles.color = color;
            DrawLine(envelope, envelope.xMin, xRange, yRange, offset, size);
        }

        private void DrawLine(Envelope envelope, float startingX, float xRange, float yRange, Vector3 offset, Vector3 size) {
            float totalXRange = (envelope.xMax - envelope.xMin);
            float endX = startingX + xRange;

            int nEval = (int)(xRange / totalXRange * size.x / 2f);
            float timeStep = xRange / nEval;
            float xStep = timeStep / totalXRange * size.x;
            float prevVal = envelope.Evaluate(startingX);

            float time = startingX + timeStep;
            for (int it = 0; it < nEval; it++) {
                float x = (time - envelope.xMin) / totalXRange * size.x;
                float val = envelope.Evaluate(time);
                float y = size.y - ((val - envelope.yMin) / yRange * size.y);
                float prevY = size.y - ((prevVal - envelope.yMin) / yRange * size.y);
                for (int i = -3; i <= 3; i++) { // for thickness since 2019.4 was not supported yet
                    Handles.DrawLine(new Vector3(x - xStep, prevY + i * 0.05f, 0) + offset, new Vector3(x, y + i * 0.05f, 0f) + offset);
                }
                prevVal = val;

                if (time < endX) {
                    time += timeStep;
                    if (time > endX) {
                        time = endX;
                    }
                }
                else {
                    time += timeStep;
                }
            }
        }

        private void DrawSelectedLineSegment(Envelope envelope, float yRange, Vector3 offset, Vector3 size, int endPointIndex) {
            Handles.color = new Color(0.1f, 0.7f, 0.9f);
            float startX = envelope.GetPoint(endPointIndex - 1).time;
            float endX = envelope.GetPoint(endPointIndex).time;
            float segmentRange = endX - startX;
            DrawLine(envelope, startX, segmentRange, yRange, offset, size);
        }

        private void DrawPoints(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size) {
            var evt = Event.current;
            envelope.ForEach((index, point) => {
                float x = (point.time - envelope.xMin) / xRange * size.x;
                float y = size.y - ((point.value - envelope.yMin) / yRange * size.y);
                var pointRect = new Rect(x + offset.x - 4, y + offset.y - 4, 8, 8);
                Color col = point == draggedPoint || pointRect.Contains(evt.mousePosition) ?
                    Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                Handles.DrawSolidRectangleWithOutline(pointRect, col, col);
            });
        }

        private void DrawSelectedPoints(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size) {
            Handles.color = new Color(0.1f, 0.75f, 0.85f);
            foreach (var index in selectedIndices) {
                var point = envelope.GetPoint(index);
                float x = (point.time - envelope.xMin) / xRange * size.x;
                float y = size.y - ((point.value - envelope.yMin) / yRange * size.y);
                var pointRect = new Rect(x + offset.x - 4, y + offset.y - 4, 8, 8);
                Handles.DrawSolidRectangleWithOutline(pointRect, Color.white, Color.white);
            }
        }

        private void DrawValueLabels(Envelope envelope, Rect waveformRect) {
            var yMinLabel = new Rect(waveformRect.x + 2f, waveformRect.position.y - 16 + waveformRect.size.y, 40, 20);
            GUI.Label(yMinLabel, envelope.yMin.ToString());

            var yMaxLabel = new Rect(waveformRect.x + 2f, waveformRect.position.y - 4, 40, 20);
            GUI.Label(yMaxLabel, envelope.yMax.ToString());
        }

        #endregion

        #region INPUT-HANDLERS

        private bool HandleMouseInput(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, bool allowAddPointByDoubleClick) {
            bool dirty = false;
            var evt = Event.current;

            if (evt.type == EventType.Used) return dirty;

            if (HandleBoxSelection(envelope, xRange, yRange, offset, size, evt)) dirty = true;
            DrawSelectedPoints(envelope, xRange, yRange, offset, size);
            if (isBoxSelecting) return dirty;

            if (HandleDraggedLine(envelope, xRange, yRange, offset, size, evt)) dirty = true;
            if (draggedLine != null) return dirty;

            if (HandleDraggedExponent(envelope, xRange, yRange, offset, size, evt)) dirty = true;
            if (draggedExponent != null) return dirty;

            if (HandleDraggedPoint(envelope, xRange, yRange, offset, size, evt)) dirty = true;
            if (draggedPoint != null) return dirty;

            bool handledAnyPoint;
            if (HandlePointsInput(envelope, xRange, yRange, offset, size, evt, out handledAnyPoint)) dirty = true;
            if (handledAnyPoint) return dirty;

            bool lineHovered;
            if (HandleLineHover(envelope, xRange, yRange, offset, size, evt, out lineHovered)) dirty = true;
            if (lineHovered) return dirty;

            if (HandleOutsideLine(envelope, xRange, yRange, offset, size, evt, allowAddPointByDoubleClick)) dirty = true;
            return dirty;
        }

        private bool HandleDraggedLine(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt) {
            bool dirty = false;
            if (draggedLine != null) {
                DrawSelectedLineSegment(envelope, yRange, offset, size, draggedLineIndex);
            }

            if ((evt.type == EventType.MouseUp && evt.button == 0) ||
                evt.type == EventType.Ignore || evt.type == EventType.DragExited ||
                !evt.shift) {
                draggedLine = null;
                draggedLineIndex = -1;
            }
            else if (evt.type == EventType.MouseDrag && draggedLine != null) {
                MovePoints(envelope, xRange, yRange, size, evt, new int[] { draggedLineIndex - 1, draggedLineIndex }, emptyIntArray);
                evt.Use();
                dirty = true;
            }
            return dirty;
        }

        private bool HandleDraggedExponent(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt) {
            bool dirty = false;

            if (draggedExponent != null) {
                DrawSelectedLineSegment(envelope, yRange, offset, size, draggedExponentIndex);
            }

            if ((evt.type == EventType.MouseUp && evt.button == 0) ||
                evt.type == EventType.Ignore || evt.type == EventType.DragExited ||
                !evt.shift) {
                draggedExponent = null;
                draggedExponentIndex = -1;
            }
            else if (evt.type == EventType.MouseDrag && draggedExponent != null) {
                float multiplier;
                if (draggedExponent.exponent == 0f) {
                    multiplier = 0.000001f;
                }
                else {
                    multiplier = Mathf.Sqrt(draggedExponent.exponent);
                }

                var delta = evt.delta.y;
                if (draggedExponent.value < envelope.GetPoint(draggedExponentIndex - 1).value) {
                    delta *= -1;
                }

                draggedExponent.exponent += delta / size.y * multiplier * 8f;
                if (draggedExponent.exponent < 0) {
                    draggedExponent.exponent = 0f;
                }
                evt.Use();
                dirty = true;
            }
            return dirty;
        }

        private void MovePoints(Envelope envelope, float xRange, float yRange, Vector3 size, Event evt, int[] indicesToMove, int[] indicesToMoveYOnly) {
            float deltaTime = xRange * evt.delta.x / size.x;
            float deltaValue = yRange * -evt.delta.y / size.y;

            var lastIndex = indicesToMove[indicesToMove.Length - 1];
            var lastPoint = envelope.GetPoint(lastIndex);
            int firstIndex = indicesToMove[0];
            var firstPoint = envelope.GetPoint(firstIndex);
            foreach (var index in indicesToMove) {
                var point = envelope.GetPoint(index);
                point.time += deltaTime;
                point.value += deltaValue;
            }
            foreach (var index in indicesToMoveYOnly) {
                var point = envelope.GetPoint(index);
                point.value += deltaValue;
            }

            if (firstIndex > 0) {
                if (firstPoint.time < envelope.GetPoint(firstIndex - 1).time) {
                    float delta = envelope.GetPoint(firstIndex - 1).time - firstPoint.time;
                    firstPoint.time = envelope.GetPoint(firstIndex - 1).time;
                    foreach (var index in indicesToMove) {
                        if (index != firstIndex) {
                            var point = envelope.GetPoint(index);
                            point.time += delta;
                        }
                    }
                }
            }
            else if (firstIndex == 0) {
                // Shouldn't move start point
                //if (firstPoint.time < envelope.xMin) {
                //    float delta = envelope.xMin - firstPoint.time;
                //    firstPoint.time = envelope.xMin;
                //    foreach (var index in indicesToMove) {
                //    if (index != firstIndex) {
                //        var point = envelope.GetPoint(index);
                //        point.time += delta;
                //    }
                //}
                //}
                foreach (var index in indicesToMove) {
                    var point = envelope.GetPoint(index);
                    point.time -= deltaTime;
                }
            }

            if (firstIndex > 0) {
                if (lastIndex < envelope.Count - 1) {
                    if (lastPoint.time > envelope.GetPoint(lastIndex + 1).time) {
                        float delta = envelope.GetPoint(lastIndex + 1).time - lastPoint.time;
                        lastPoint.time = envelope.GetPoint(lastIndex + 1).time;
                        foreach (var index in indicesToMove) {
                            if (index != lastIndex) {
                                var point = envelope.GetPoint(index);
                                point.time += delta;
                            }
                        }
                    }
                }
                else if (lastIndex == envelope.Count - 1) {
                    if (Envelope.requiresEndPoint) {
                        float delta = envelope.xMax - lastPoint.time;
                        lastPoint.time = envelope.xMax;
                        foreach (var index in indicesToMove) {
                            if (index != lastIndex) {
                                var point = envelope.GetPoint(index);
                                point.time += delta;
                            }
                        }
                    }
                    else {
                        if (lastPoint.time > envelope.xMax) {
                            float delta = envelope.xMax - lastPoint.time;
                            lastPoint.time = envelope.xMax;
                            foreach (var index in indicesToMove) {
                                if (index != lastIndex) {
                                    var point = envelope.GetPoint(index);
                                    point.time += delta;
                                }
                            }
                        }
                    }
                }
            }

            int highestIndex = -1;
            float highestVal = float.MinValue;
            int lowestIndex = -1;
            float lowestVal = float.MaxValue;
            foreach (var index in indicesToMove) {
                var point = envelope.GetPoint(index);
                if (point.value < lowestVal) {
                    lowestVal = point.value;
                    lowestIndex = index;
                }
                if (point.value > highestVal) {
                    highestVal = point.value;
                    highestIndex = index;
                }
            }
            foreach (var index in indicesToMoveYOnly) {
                var point = envelope.GetPoint(index);
                if (point.value < lowestVal) {
                    lowestVal = point.value;
                    lowestIndex = index;
                }
                if (point.value > highestVal) {
                    highestVal = point.value;
                    highestIndex = index;
                }
            }

            if (highestVal > envelope.yMax) {
                float delta = envelope.yMax - highestVal;
                envelope.GetPoint(highestIndex).value = envelope.yMax;
                foreach (var index in indicesToMove) {
                    if (index != highestIndex) {
                        var point = envelope.GetPoint(index);
                        point.value += delta;
                    }
                }
                foreach (var index in indicesToMoveYOnly) {
                    if (index != highestIndex) {
                        var point = envelope.GetPoint(index);
                        point.value += delta;
                    }
                }
            }
            else if (lowestVal < envelope.yMin) {
                float delta = envelope.yMin - lowestVal;
                envelope.GetPoint(lowestIndex).value = envelope.yMin;
                foreach (var index in indicesToMove) {
                    if (index != lowestIndex) {
                        var point = envelope.GetPoint(index);
                        point.value += delta;
                    }
                }
                foreach (var index in indicesToMoveYOnly) {
                    if (index != lowestIndex) {
                        var point = envelope.GetPoint(index);
                        point.value += delta;
                    }
                }
            }
        }

        private bool HandleDraggedPoint(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt) {
            bool dirty = false;
            if (evt.type == EventType.MouseDrag && draggedPoint != null) {
                float time = xRange * (evt.mousePosition.x - offset.x) / size.x;
                float val = yRange - (yRange * (evt.mousePosition.y - offset.y)) / size.y;
                time += envelope.xMin;
                val += envelope.yMin;

                if (draggedPointIndex > 0) {
                    if (time < envelope.GetPoint(draggedPointIndex - 1).time) {
                        time = envelope.GetPoint(draggedPointIndex - 1).time;
                    }
                }
                else if (draggedPointIndex == 0) {
                    time = envelope.xMin;
                }

                if (draggedPointIndex < envelope.Count - 1) {
                    if (time > envelope.GetPoint(draggedPointIndex + 1).time) {
                        time = envelope.GetPoint(draggedPointIndex + 1).time;
                    }
                }
                else if (draggedPointIndex == envelope.Count - 1) {
                    if (Envelope.requiresEndPoint) {
                        time = envelope.xMax;
                    }
                    else {
                        if (time > envelope.xMax) {
                            time = envelope.xMax;
                        }
                    }
                }

                if (val > envelope.yMax) {
                    val = envelope.yMax;
                }
                else if (val < envelope.yMin) {
                    val = envelope.yMin;
                }

                draggedPoint.time = time;
                draggedPoint.value = val;
                evt.Use();
                dirty = true;
            }
            else if ((evt.type == EventType.MouseUp && evt.button == 0) ||
                evt.type == EventType.Ignore || evt.type == EventType.DragExited) {
                draggedPoint = null;
                draggedPointIndex = -1;
                if (lastActiveGUI == this) lastActiveGUI = null;
            }

            return dirty;
        }

        private bool HandlePointsInput(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt, out bool handled) {
            bool dirty = false;
            bool isHandled = false;
            envelope.ForEach((index, point) => {
                float x = (point.time - envelope.xMin) / xRange * size.x;
                float y = size.y - ((point.value - envelope.yMin) / yRange * size.y);
                var pointRect = new Rect(x + offset.x - 4, y + offset.y - 4, 8, 8);
                if (pointRect.Contains(evt.mousePosition)) {
                    isHandled = true;

                    if (draggedPoint == null) {
                        if (evt.type == EventType.MouseDown && evt.button == 0) {
                            if (evt.clickCount == 1) {
                                lastActiveGUI = this;
                                draggedPoint = point;
                                draggedPointIndex = envelope.IndexOf(draggedPoint);

                                if (!selectedIndices.Contains(draggedPointIndex)) {
                                    selectedIndices.Clear();
                                    selectedIndices.Add(draggedPointIndex);
                                    Debug.Log("Unfocus");
                                    GUI.FocusControl(null);
                                }
                            }
                            else if (evt.clickCount == 2) {
                                selectedIndices.Clear();
                                envelope.RemovePoint(point);
                                dirty = true;
                            }
                        }
                    }
                }
            });

            handled = isHandled;
            return dirty;
        }

        private bool IsHoveringLine(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt) {
            if (evt.mousePosition.x > offset.x && evt.mousePosition.y > offset.y &&
                            evt.mousePosition.x <= offset.x + size.x && evt.mousePosition.y <= offset.y + size.y) {
                float x = evt.mousePosition.x - offset.x;
                float y = evt.mousePosition.y - offset.y;
                float time = envelope.xMin + (xRange * x / size.x);
                float targetVal = envelope.Evaluate(time);
                float targetY = size.y - ((targetVal - envelope.yMin) / yRange * size.y);
                if (Mathf.Abs(y - targetY) <= 4) {
                    return true;
                }
            }
            return false;
        }

        private bool HandleLineHover(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt, out bool handled) {
            bool dirty = false;
            handled = false;
            if (evt.mousePosition.x > offset.x && evt.mousePosition.y > offset.y &&
                            evt.mousePosition.x <= offset.x + size.x && evt.mousePosition.y <= offset.y + size.y) {
                float x = evt.mousePosition.x - offset.x;
                float y = evt.mousePosition.y - offset.y;
                float time = envelope.xMin + (xRange * x / size.x);
                float targetVal = envelope.Evaluate(time);
                float targetY = size.y - ((targetVal - envelope.yMin) / yRange * size.y);
                if (Mathf.Abs(y - targetY) <= 4) {
                    var pointRect = new Rect(x + offset.x - 4, targetY + offset.y - 4, 8, 8);
                    if (evt.shift) {    // Move existing line
                        int closestIndex = envelope.GetClosestIndexCeil(time);
                        if (closestIndex > 0 && closestIndex < envelope.Count) {
                            DrawSelectedLineSegment(envelope, yRange, offset, size, closestIndex);

                            if (evt.type == EventType.MouseDown) {
                                if (evt.button == 0) {
                                    draggedLineIndex = closestIndex;
                                    draggedLine = envelope.GetPoint(closestIndex);
                                    selectedIndices.Clear();
                                }
                                else if (evt.button == 1) {
                                    draggedExponentIndex = closestIndex;
                                    draggedExponent = envelope.GetPoint(closestIndex);
                                    selectedIndices.Clear();
                                }
                            }
                        }
                    }
                    else {  // Add new point
                        Handles.color = Color.white;
                        Color col = new Color(1f, 1f, 1f, 0.5f);
                        Handles.DrawSolidRectangleWithOutline(pointRect, col, col);

                        if (evt.type == EventType.MouseDown && evt.button == 0) {
                            draggedPoint = envelope.AddPoint(time, targetVal);
                            draggedPointIndex = envelope.IndexOf(draggedPoint);
                            selectedIndices.Clear();
                            selectedIndices.Add(draggedPointIndex);
                            Debug.Log("Unfocus");
                            GUI.FocusControl(null);
                            dirty = true;
                        }
                    }
                    handled = true;
                }
            }
            return dirty;
        }

        private bool HandleOutsideLine(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt, bool allowAddPointByDoubleClick) {
            bool dirty = false;
            if (evt.type == EventType.MouseDown && evt.button == 0) {
                if (evt.mousePosition.x > offset.x && evt.mousePosition.y > offset.y &&
                    evt.mousePosition.x <= offset.x + size.x && evt.mousePosition.y <= offset.y + size.y) {
                    float x = evt.mousePosition.x - offset.x;
                    float y = evt.mousePosition.y - offset.y;

                    if (allowAddPointByDoubleClick && evt.clickCount == 2) {
                        // Add point by double-clicking
                        float time = envelope.xMin + (xRange * x / size.x);
                        float val = yRange - (yRange * y) / size.y;
                        envelope.AddPoint(time, val);
                        dirty = true;
                    }
                    else {
                        // Start box selecting
                        isBoxSelecting = true;
                        startBoxPos = new Vector2(x, y);
                        selectedIndices.Clear();
                        Debug.Log("Unfocus");
                        GUI.FocusControl(null);
                    }
                }
            }
            return dirty;
        }

        private bool HandleBoxSelection(Envelope envelope, float xRange, float yRange, Vector3 offset, Vector3 size, Event evt) {
            bool dirty = false;
            if (isBoxSelecting) {
                if (lastActiveGUI == null || lastActiveGUI == this) {
                    float currentX = evt.mousePosition.x - offset.x;
                    float currentY = evt.mousePosition.y - offset.y;

                    if (currentX > size.x) currentX = size.x;
                    else if (currentX < 0) currentX = 0;
                    if (currentY > size.y) currentY = size.y;
                    if (currentY < 0) currentY = 0;

                    float startTime = envelope.xMin + (xRange * startBoxPos.x / size.x);
                    float startVal = yRange - (yRange * startBoxPos.y) / size.y;
                    float time = envelope.xMin + (xRange * currentX / size.x);
                    float val = yRange - (yRange * currentY) / size.y;

                    float minTime = Mathf.Min(startTime, time);
                    float maxTime = Mathf.Max(startTime, time);
                    float minVal = Mathf.Min(startVal, val);
                    float maxVal = Mathf.Max(startVal, val);

                    selectedIndices.Clear();
                    envelope.ForEach((index, point) => {
                        if (point.time >= minTime && point.time <= maxTime &&
                            point.value >= minVal && point.value <= maxVal) {
                            selectedIndices.Add(index);
                        }
                    });

                    Handles.color = new Color(0.1f, 0.75f, 0.85f);
                    Handles.DrawSolidRectangleWithOutline(
                        new Rect(startBoxPos.x + offset.x, startBoxPos.y + offset.y, currentX - startBoxPos.x, currentY - startBoxPos.y),
                        new Color(1, 1, 1, 0.1f), Color.white);
                }
            }
            else {
                if (selectedIndices.Count > 1 && evt.type == EventType.MouseDrag) {
                    var includedIndices = new List<int>();
                    includedIndices.AddRange(selectedIndices);
                    var moveYOnlyIndices = new List<int>();
                    if (includedIndices.Remove(0)) {
                        moveYOnlyIndices.Add(0);
                    }
                    if (Envelope.requiresEndPoint) {
                        if (includedIndices.Remove(envelope.Count - 1)) {
                            moveYOnlyIndices.Add(envelope.Count - 1);
                        }
                    }
                    MovePoints(envelope, xRange, yRange, size, evt, includedIndices.ToArray(), moveYOnlyIndices.ToArray());
                    evt.Use();
                    dirty = true;
                }
            }

            if ((evt.type == EventType.MouseUp && evt.button == 0) ||
                evt.type == EventType.Ignore || evt.type == EventType.DragExited) {
                isBoxSelecting = false;
            }
            //else if (evt.type == EventType.MouseDrag && draggedLine != null) {

            //}

            return dirty;
        }

        private bool HandleKeyboardInput(Envelope envelope) {
            bool dirty = false;
            var evt = Event.current;
            if (evt.type == EventType.KeyDown) {
                if (evt.keyCode == KeyCode.Delete) {
                    if (selectedIndices != null) {
                        var pointsToDelete = new List<Envelope.Point>();
                        foreach (var index in selectedIndices) {
                            pointsToDelete.Add(envelope.GetPoint(index));
                        }
                        foreach (var point in pointsToDelete) {
                            envelope.RemovePoint(point);
                        }
                        selectedIndices.Clear();
                        dirty = true;
                    }
                }
            }
            return dirty;
        }
        #endregion

    }

}
