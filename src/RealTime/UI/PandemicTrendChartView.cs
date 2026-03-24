// <copyright file="PandemicTrendChartView.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.UI
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using ColossalFramework.UI;
    using RealTime.Pandemic;
    using UnityEngine;

    internal sealed class PandemicTrendChartView
    {
        private const float TopBarHeight = 26f;
        private const float PlotFramePaddingTop = 2f;
        private const float PlotLeftMargin = 52f;
        private const float PlotTopMargin = 22f;
        private const float PlotRightMargin = 12f;
        private const float PlotBottomMargin = 28f;
        private const float TooltipWidth = 210f;
        private const float TooltipHeight = 42f;
        private const float HoverThreshold = 10f;
        private const float MarkerLabelSpacing = 18f;
        private static readonly Color32 PlotBackgroundColor = new Color32(255, 249, 234, 32);
        private static readonly Color32 GridColor = new Color32(128, 128, 128, 52);
        private static readonly Color32 AxisColor = new Color32(186, 186, 186, 220);
        private static readonly Color32 FillColor = new Color32(255, 230, 160, 192);
        private static readonly Color32 LineColor = new Color32(235, 112, 94, 255);
        private static readonly Color32 PointColor = new Color32(235, 112, 94, 255);
        private static readonly Color32 HoverColor = new Color32(255, 255, 255, 220);
        private static readonly Color32 SelectionBandColor = new Color32(74, 144, 226, 62);
        private static readonly Color32 SelectionLineColor = new Color32(90, 170, 255, 220);

        private readonly List<UIPanel> fillSlices = new List<UIPanel>();
        private readonly List<UIPanel> lineSlices = new List<UIPanel>();
        private readonly List<UISprite> pointMarkers = new List<UISprite>();
        private readonly List<UIPanel> policyLines = new List<UIPanel>();
        private readonly List<UILabel> policyLabels = new List<UILabel>();
        private readonly List<UILabel> yAxisLabels = new List<UILabel>();
        private readonly List<UIPanel> yAxisGridLines = new List<UIPanel>();
        private readonly List<UILabel> xAxisLabels = new List<UILabel>();
        private readonly List<UIPanel> xAxisGridLines = new List<UIPanel>();
        private readonly List<RenderedChartPoint> renderedPoints = new List<RenderedChartPoint>();
        private readonly List<RenderedPolicyMarker> renderedMarkers = new List<RenderedPolicyMarker>();

        private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
        private PandemicChartTimeRange selectedRange = PandemicChartTimeRange.All;
        private PandemicLiveSnapshot currentSnapshot;
        private DateTime currentRangeStart;
        private DateTime currentRangeEnd;
        private float currentMinValue;
        private float currentMaxValue = 10f;
        private bool isSelecting;
        private bool hasCustomSelection;
        private float selectionAnchorX;
        private float selectionCurrentX;
        private DateTime selectionStartTime;
        private DateTime selectionEndTime;
        private IList<PandemicChartPointSnapshot> currentVisiblePoints = new List<PandemicChartPointSnapshot>();

        private UIPanel root;
        private UILabel titleLabel;
        private UILabel growthLabel;
        private UIPanel plotFrame;
        private UIPanel plotArea;
        private UIPanel selectionCapture;
        private UIPanel selectionBand;
        private UIPanel selectionStartLine;
        private UIPanel selectionEndLine;
        private UILabel emptyLabel;
        private UIPanel xAxisLine;
        private UIPanel yAxisLine;
        private UILabel xAxisTitleLabel;
        private UILabel yAxisTitleLabel;
        private UIPanel hoverLine;
        private UISprite hoverPoint;
        private UIPanel tooltipPanel;
        private UILabel tooltipLabel;
        private UIButton range24hButton;
        private UIButton range7dButton;
        private UIButton range30dButton;
        private UIButton rangeAllButton;

        public UIPanel Component => root;

        public float Height => root?.height ?? 0f;

        public void Initialize(UIComponent parent, float width, float height)
        {
            root = parent.AddUIComponent<UIPanel>();
            root.autoSize = false;
            root.width = width;
            root.height = height;
            root.autoLayout = false;
            root.clipChildren = false;

            titleLabel = root.AddUIComponent<UILabel>();
            titleLabel.autoSize = false;
            titleLabel.width = 160f;
            titleLabel.height = TopBarHeight;
            titleLabel.relativePosition = new Vector3(0f, 2f);
            titleLabel.text = "Infected Trend";
            titleLabel.textScale = 0.82f;
            titleLabel.textColor = new Color32(245, 245, 245, 255);

            growthLabel = root.AddUIComponent<UILabel>();
            growthLabel.autoSize = false;
            growthLabel.width = 220f;
            growthLabel.height = TopBarHeight;
            growthLabel.relativePosition = new Vector3(168f, 2f);
            growthLabel.textScale = 0.74f;
            growthLabel.textAlignment = UIHorizontalAlignment.Left;
            growthLabel.textColor = new Color32(235, 235, 235, 255);

            float buttonWidth = 44f;
            float buttonGap = 4f;
            float buttonStartX = width - ((buttonWidth * 4f) + (buttonGap * 3f));
            range24hButton = CreateRangeButton("24h", buttonStartX, 0f, buttonWidth);
            range7dButton = CreateRangeButton("7d", buttonStartX + buttonWidth + buttonGap, 0f, buttonWidth);
            range30dButton = CreateRangeButton("30d", buttonStartX + ((buttonWidth + buttonGap) * 2f), 0f, buttonWidth);
            rangeAllButton = CreateRangeButton("All", buttonStartX + ((buttonWidth + buttonGap) * 3f), 0f, buttonWidth);
            range24hButton.eventClicked += (c, e) => SetRange(PandemicChartTimeRange.Hours24);
            range7dButton.eventClicked += (c, e) => SetRange(PandemicChartTimeRange.Days7);
            range30dButton.eventClicked += (c, e) => SetRange(PandemicChartTimeRange.Days30);
            rangeAllButton.eventClicked += (c, e) => SetRange(PandemicChartTimeRange.All);

            plotFrame = root.AddUIComponent<UIPanel>();
            plotFrame.autoSize = false;
            plotFrame.width = width;
            plotFrame.height = height - TopBarHeight - PlotFramePaddingTop;
            plotFrame.relativePosition = new Vector3(0f, TopBarHeight + PlotFramePaddingTop);
            plotFrame.backgroundSprite = "MenuPanel2";
            plotFrame.opacity = 0.6f;
            plotFrame.clipChildren = false;

            yAxisTitleLabel = plotFrame.AddUIComponent<UILabel>();
            yAxisTitleLabel.autoSize = false;
            yAxisTitleLabel.width = 180f;
            yAxisTitleLabel.height = 16f;
            yAxisTitleLabel.relativePosition = new Vector3(8f, 4f);
            yAxisTitleLabel.text = "Infected citizens";
            yAxisTitleLabel.textScale = 0.62f;
            yAxisTitleLabel.textColor = new Color32(228, 228, 228, 255);

            plotArea = plotFrame.AddUIComponent<UIPanel>();
            plotArea.autoSize = false;
            plotArea.width = width - PlotLeftMargin - PlotRightMargin;
            plotArea.height = plotFrame.height - PlotTopMargin - PlotBottomMargin;
            plotArea.relativePosition = new Vector3(PlotLeftMargin, PlotTopMargin);
            plotArea.backgroundSprite = "EmptySprite";
            plotArea.color = PlotBackgroundColor;
            plotArea.opacity = 1f;
            plotArea.clipChildren = true;
            plotArea.isInteractive = false;

            xAxisLine = CreateSolidPanel(plotArea, AxisColor);
            yAxisLine = CreateSolidPanel(plotArea, AxisColor);

            selectionBand = CreateSolidPanel(plotArea, SelectionBandColor);
            selectionBand.zOrder = 4;
            selectionStartLine = CreateSolidPanel(plotArea, SelectionLineColor);
            selectionStartLine.width = 2f;
            selectionStartLine.zOrder = 5;
            selectionEndLine = CreateSolidPanel(plotArea, SelectionLineColor);
            selectionEndLine.width = 2f;
            selectionEndLine.zOrder = 5;

            selectionCapture = plotFrame.AddUIComponent<UIPanel>();
            selectionCapture.autoSize = false;
            selectionCapture.width = plotArea.width;
            selectionCapture.height = plotArea.height + PlotBottomMargin;
            selectionCapture.relativePosition = plotArea.relativePosition;
            selectionCapture.backgroundSprite = "EmptySprite";
            selectionCapture.opacity = 0f;
            selectionCapture.isInteractive = true;
            selectionCapture.eventMouseDown += OnSelectionMouseDown;
            selectionCapture.eventMouseMove += OnSelectionMouseMove;
            selectionCapture.eventMouseUp += OnSelectionMouseUp;
            selectionCapture.eventMouseLeave += OnSelectionMouseLeave;

            emptyLabel = plotFrame.AddUIComponent<UILabel>();
            emptyLabel.autoSize = false;
            emptyLabel.width = plotArea.width;
            emptyLabel.height = 24f;
            emptyLabel.relativePosition = new Vector3(plotArea.relativePosition.x, plotArea.relativePosition.y + (plotArea.height / 2f) - 12f);
            emptyLabel.textScale = 0.75f;
            emptyLabel.textAlignment = UIHorizontalAlignment.Center;
            emptyLabel.textColor = new Color32(235, 235, 235, 255);

            xAxisTitleLabel = plotFrame.AddUIComponent<UILabel>();
            xAxisTitleLabel.autoSize = false;
            xAxisTitleLabel.width = 160f;
            xAxisTitleLabel.height = 16f;
            xAxisTitleLabel.relativePosition = new Vector3(plotArea.relativePosition.x + (plotArea.width / 2f) - 80f, plotArea.relativePosition.y + plotArea.height + 10f);
            xAxisTitleLabel.text = "Simulation time";
            xAxisTitleLabel.textScale = 0.62f;
            xAxisTitleLabel.textAlignment = UIHorizontalAlignment.Center;
            xAxisTitleLabel.textColor = new Color32(228, 228, 228, 255);

            hoverLine = CreateSolidPanel(plotArea, HoverColor);
            hoverLine.width = 1f;
            hoverLine.height = plotArea.height;
            hoverLine.isVisible = false;

            hoverPoint = plotArea.AddUIComponent<UISprite>();
            hoverPoint.autoSize = false;
            hoverPoint.width = 8f;
            hoverPoint.height = 8f;
            hoverPoint.spriteName = "ScrollbarThumb";
            hoverPoint.color = HoverColor;
            hoverPoint.isVisible = false;

            tooltipPanel = plotFrame.AddUIComponent<UIPanel>();
            tooltipPanel.autoSize = false;
            tooltipPanel.width = TooltipWidth;
            tooltipPanel.height = TooltipHeight;
            tooltipPanel.backgroundSprite = "MenuPanel2";
            tooltipPanel.opacity = 0.95f;
            tooltipPanel.clipChildren = true;
            tooltipPanel.isVisible = false;

            tooltipLabel = tooltipPanel.AddUIComponent<UILabel>();
            tooltipLabel.autoSize = false;
            tooltipLabel.width = TooltipWidth - 12f;
            tooltipLabel.height = TooltipHeight - 8f;
            tooltipLabel.relativePosition = new Vector3(6f, 4f);
            tooltipLabel.textScale = 0.68f;
            tooltipLabel.textColor = new Color32(245, 245, 245, 255);

            UpdateRangeButtons();
        }

        public void Refresh(PandemicLiveSnapshot snapshot, CultureInfo culture)
        {
            cultureInfo = culture ?? CultureInfo.CurrentCulture;
            currentSnapshot = snapshot;
            UpdateRangeButtons();
            Render(snapshot);
        }

        private void SetRange(PandemicChartTimeRange range)
        {
            if (selectedRange == range)
            {
                return;
            }

            selectedRange = range;
            ClearCustomSelection();
            UpdateRangeButtons();
            Render(currentSnapshot);
        }

        private void Render(PandemicLiveSnapshot snapshot)
        {
            HideHover();
            renderedPoints.Clear();
            renderedMarkers.Clear();
            HidePolicyWidgets();
            HideSeriesWidgets();
            HideAxisWidgets();

            if (snapshot == null || snapshot.ChartPoints.Count == 0)
            {
                currentVisiblePoints = new List<PandemicChartPointSnapshot>();
                RenderEmptyState(snapshot != null && snapshot.LifecycleState == PandemicLifecycleState.Dormant
                    ? "Pandemic not started yet."
                    : "Waiting for chart data.");
                return;
            }

            List<PandemicChartPointSnapshot> orderedPoints = snapshot.ChartPoints
                .OrderBy(point => point.SimulationTime)
                .ToList();
            if (orderedPoints.Count == 0)
            {
                currentVisiblePoints = new List<PandemicChartPointSnapshot>();
                RenderEmptyState("Waiting for chart data.");
                return;
            }

            DateTime rangeStart;
            DateTime rangeEnd;
            List<PandemicChartPointSnapshot> visiblePoints = FilterPoints(orderedPoints, out rangeStart, out rangeEnd);
            if (visiblePoints.Count == 0)
            {
                currentVisiblePoints = new List<PandemicChartPointSnapshot>();
                RenderEmptyState("Waiting for chart data.");
                return;
            }

            if (rangeEnd <= rangeStart)
            {
                rangeEnd = rangeStart.AddHours(1);
            }

            currentRangeStart = rangeStart;
            currentRangeEnd = rangeEnd;
            currentVisiblePoints = visiblePoints;
            NormalizeSelectionToVisibleRange();

            List<PandemicPolicyMarkerSnapshot> visibleMarkers = FilterMarkers(snapshot.PolicyMarkers, rangeStart, rangeEnd);
            ComputeAxisBounds(visiblePoints, out float minValue, out float maxValue);
            currentMinValue = minValue;
            currentMaxValue = maxValue;
            BuildRenderedPoints(visiblePoints, rangeStart, rangeEnd, minValue, maxValue);
            RenderAxes(rangeStart, rangeEnd, minValue, maxValue);
            RenderSeries(visiblePoints, rangeStart, rangeEnd, minValue, maxValue);
            RenderPolicyMarkers(visibleMarkers, rangeStart, rangeEnd);
            RenderSelectionOverlay();
            UpdateGrowth();
            emptyLabel.isVisible = false;
        }

        private void RenderEmptyState(string text)
        {
            emptyLabel.text = text;
            emptyLabel.isVisible = true;
            growthLabel.text = "Growth: -";
            ClearSelectionOverlay();
        }

        private void BuildRenderedPoints(
            IList<PandemicChartPointSnapshot> visiblePoints,
            DateTime rangeStart,
            DateTime rangeEnd,
            float minValue,
            float maxValue)
        {
            double ticksRange = Math.Max(1d, (double)(rangeEnd.Ticks - rangeStart.Ticks));
            for (int i = 0; i < visiblePoints.Count; i++)
            {
                PandemicChartPointSnapshot point = visiblePoints[i];
                float x = (float)(((point.SimulationTime.Ticks - rangeStart.Ticks) / ticksRange) * plotArea.width);
                float y = GetYPosition(point.InfectedCount, minValue, maxValue);
                int delta = i > 0 ? point.InfectedCount - visiblePoints[i - 1].InfectedCount : 0;
                renderedPoints.Add(new RenderedChartPoint
                {
                    Point = point,
                    X = x,
                    Y = y,
                    Delta = delta,
                });
            }
        }

        private void RenderAxes(DateTime startTime, DateTime endTime, float minValue, float maxValue)
        {
            xAxisLine.isVisible = true;
            xAxisLine.relativePosition = new Vector3(0f, plotArea.height - 1f);
            xAxisLine.width = plotArea.width;
            xAxisLine.height = 1f;

            yAxisLine.isVisible = true;
            yAxisLine.relativePosition = Vector3.zero;
            yAxisLine.width = 1f;
            yAxisLine.height = plotArea.height;

            float span = Mathf.Max(1f, maxValue - minValue);
            float step = GetNiceStep(span / 4f);
            int yTickCount = Math.Max(2, Mathf.CeilToInt(span / step) + 1);
            EnsureYAxisWidgets(yTickCount);

            for (int i = 0; i < yTickCount; i++)
            {
                float value = minValue + (i * step);
                if (value > maxValue + 0.001f)
                {
                    value = maxValue;
                }

                float y = GetYPosition(value, minValue, maxValue);

                UIPanel gridLine = yAxisGridLines[i];
                gridLine.isVisible = true;
                gridLine.relativePosition = new Vector3(0f, Mathf.Clamp(y, 0f, plotArea.height - 1f));
                gridLine.width = plotArea.width;
                gridLine.height = 1f;

                UILabel label = yAxisLabels[i];
                label.isVisible = true;
                label.relativePosition = new Vector3(2f, plotArea.relativePosition.y + Mathf.Clamp(y - 7f, -4f, plotArea.height - 14f));
                label.text = value.ToString("N0", cultureInfo);
            }

            for (int i = yTickCount; i < yAxisGridLines.Count; i++)
            {
                yAxisGridLines[i].isVisible = false;
                yAxisLabels[i].isVisible = false;
            }

            TimeSpan visibleSpan = endTime - startTime;
            int xTickCount = visibleSpan <= TimeSpan.FromDays(1) ? 6 : 5;
            EnsureXAxisWidgets(xTickCount);
            for (int i = 0; i < xTickCount; i++)
            {
                float ratio = xTickCount == 1 ? 0f : i / (float)(xTickCount - 1);
                float x = ratio * plotArea.width;
                DateTime labelTime = new DateTime(startTime.Ticks + (long)((endTime.Ticks - startTime.Ticks) * ratio));

                UIPanel gridLine = xAxisGridLines[i];
                gridLine.isVisible = true;
                gridLine.relativePosition = new Vector3(Mathf.Clamp(x, 0f, plotArea.width - 1f), 0f);
                gridLine.width = 1f;
                gridLine.height = plotArea.height;

                UILabel label = xAxisLabels[i];
                label.isVisible = true;
                label.relativePosition = new Vector3(
                    plotArea.relativePosition.x + Mathf.Clamp(x - 28f, 0f, plotArea.width - 56f),
                    plotArea.relativePosition.y + plotArea.height + 2f);
                label.text = FormatXAxisLabel(labelTime, visibleSpan);
            }

            for (int i = xTickCount; i < xAxisGridLines.Count; i++)
            {
                xAxisGridLines[i].isVisible = false;
                xAxisLabels[i].isVisible = false;
            }
        }

        private void RenderSeries(
            IList<PandemicChartPointSnapshot> visiblePoints,
            DateTime rangeStart,
            DateTime rangeEnd,
            float minValue,
            float maxValue)
        {
            int sliceCount = Mathf.Clamp(Mathf.RoundToInt(plotArea.width / 1.5f), 180, 520);
            EnsureSeriesWidgets(sliceCount, visiblePoints.Count);
            double ticksRange = Math.Max(1d, (double)(rangeEnd.Ticks - rangeStart.Ticks));
            int segmentIndex = 0;

            for (int i = 0; i < sliceCount; i++)
            {
                float ratio = sliceCount == 1 ? 0f : i / (float)(sliceCount - 1);
                long ticks = rangeStart.Ticks + (long)(ticksRange * ratio);

                while (segmentIndex < visiblePoints.Count - 2 && visiblePoints[segmentIndex + 1].SimulationTime.Ticks < ticks)
                {
                    segmentIndex++;
                }

                PandemicChartPointSnapshot startPoint = visiblePoints[segmentIndex];
                PandemicChartPointSnapshot endPoint = visiblePoints[Math.Min(segmentIndex + 1, visiblePoints.Count - 1)];
                float interpolatedValue = InterpolateValue(startPoint, endPoint, ticks);
                float y = GetYPosition(interpolatedValue, minValue, maxValue);
                float sliceX = ratio * plotArea.width;
                float sliceWidth = Mathf.Max(1f, plotArea.width / sliceCount + 0.75f);

                UIPanel fillSlice = fillSlices[i];
                fillSlice.isVisible = true;
                fillSlice.relativePosition = new Vector3(sliceX, y);
                fillSlice.width = sliceWidth;
                fillSlice.height = Mathf.Max(1f, plotArea.height - y);

                UIPanel lineSlice = lineSlices[i];
                lineSlice.isVisible = true;
                lineSlice.relativePosition = new Vector3(sliceX, Mathf.Clamp(y - 1f, 0f, plotArea.height - 2f));
                lineSlice.width = sliceWidth;
                lineSlice.height = 2f;
            }

            for (int i = sliceCount; i < fillSlices.Count; i++)
            {
                fillSlices[i].isVisible = false;
                lineSlices[i].isVisible = false;
            }

            int pointStep = Math.Max(1, Mathf.CeilToInt(renderedPoints.Count / 12f));
            int markerIndex = 0;
            for (int i = 0; i < renderedPoints.Count; i += pointStep)
            {
                if (markerIndex >= pointMarkers.Count)
                {
                    break;
                }

                RenderedChartPoint point = renderedPoints[i];
                UISprite marker = pointMarkers[markerIndex++];
                marker.isVisible = true;
                marker.relativePosition = new Vector3(
                    Mathf.Clamp(point.X - 3f, 0f, plotArea.width - 6f),
                    Mathf.Clamp(point.Y - 3f, 0f, plotArea.height - 6f));
                marker.tooltip = BuildPointTooltip(point);
            }

            for (int i = markerIndex; i < pointMarkers.Count; i++)
            {
                pointMarkers[i].isVisible = false;
            }
        }

        private void RenderPolicyMarkers(IList<PandemicPolicyMarkerSnapshot> markers, DateTime startTime, DateTime endTime)
        {
            if (markers == null || markers.Count == 0)
            {
                return;
            }

            EnsurePolicyWidgets(markers.Count);
            double ticksRange = Math.Max(1d, (double)(endTime.Ticks - startTime.Ticks));
            float lastLabelX = float.MinValue;

            for (int i = 0; i < markers.Count; i++)
            {
                PandemicPolicyMarkerSnapshot marker = markers[i];
                float x = (float)(((marker.SimulationTime.Ticks - startTime.Ticks) / ticksRange) * plotArea.width);
                renderedMarkers.Add(new RenderedPolicyMarker
                {
                    Marker = marker,
                    X = x,
                });

                UIPanel line = policyLines[i];
                line.isVisible = true;
                line.color = GetMarkerColor(marker);
                line.relativePosition = new Vector3(Mathf.Clamp(x, 0f, plotArea.width - 1f), 0f);
                line.width = 1f;
                line.height = plotArea.height;

                UILabel label = policyLabels[i];
                label.text = marker.ShortLabel;
                label.textColor = GetMarkerColor(marker);
                label.relativePosition = new Vector3(
                    plotArea.relativePosition.x + Mathf.Clamp(x - 8f, 0f, plotArea.width - 18f),
                    Mathf.Max(0f, plotArea.relativePosition.y - 14f));
                bool showLabel = x - lastLabelX >= MarkerLabelSpacing;
                label.isVisible = showLabel;
                if (showLabel)
                {
                    lastLabelX = x;
                }
            }

            for (int i = markers.Count; i < policyLines.Count; i++)
            {
                policyLines[i].isVisible = false;
                policyLabels[i].isVisible = false;
            }
        }

        private void UpdateGrowth()
        {
            IList<PandemicChartPointSnapshot> points = GetGrowthPoints();
            if (points == null || points.Count == 0)
            {
                growthLabel.text = "Growth: -";
                return;
            }

            int first = points[0].InfectedCount;
            int last = points[points.Count - 1].InfectedCount;
            int delta = last - first;
            string deltaText = FormatSigned(delta);
            string percentText;
            if (first <= 0)
            {
                percentText = last > 0 ? "n/a" : "0.0%";
            }
            else
            {
                float percent = (delta * 100f) / first;
                percentText = percent.ToString("+0.0;-0.0;0.0", cultureInfo) + "%";
            }

            growthLabel.text = (hasCustomSelection ? "Growth (selection): " : "Growth: ") + percentText + " (" + deltaText + ")";
        }

        private void OnSelectionMouseDown(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (currentVisiblePoints == null || currentVisiblePoints.Count == 0)
            {
                return;
            }

            isSelecting = true;
            selectionAnchorX = eventParam.position.x - plotArea.absolutePosition.x;
            selectionCurrentX = selectionAnchorX;
            hasCustomSelection = false;
            RenderSelectionOverlay();
        }

        private void OnSelectionMouseMove(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (renderedPoints.Count == 0)
            {
                HideHover();
                return;
            }

            float localX = eventParam.position.x - plotArea.absolutePosition.x;
            float localY = eventParam.position.y - plotArea.absolutePosition.y;
            if (isSelecting)
            {
                selectionCurrentX = Mathf.Clamp(localX, 0f, plotArea.width);
                selectionStartTime = GetTimeForX(Mathf.Min(selectionAnchorX, selectionCurrentX));
                selectionEndTime = GetTimeForX(Mathf.Max(selectionAnchorX, selectionCurrentX));
                hasCustomSelection = Mathf.Abs(selectionCurrentX - selectionAnchorX) >= 8f;
                RenderSelectionOverlay();
                UpdateGrowth();
            }

            if (localX < 0f || localY < 0f || localX > plotArea.width || localY > plotArea.height)
            {
                HideHover();
                return;
            }

            RenderedChartPoint nearest = renderedPoints[0];
            float nearestDistance = Mathf.Abs(nearest.X - localX);
            for (int i = 1; i < renderedPoints.Count; i++)
            {
                float distance = Mathf.Abs(renderedPoints[i].X - localX);
                if (distance < nearestDistance)
                {
                    nearest = renderedPoints[i];
                    nearestDistance = distance;
                }
            }

            hoverLine.isVisible = true;
            hoverLine.relativePosition = new Vector3(Mathf.Clamp(nearest.X, 0f, plotArea.width - 1f), 0f);
            hoverLine.height = plotArea.height;

            hoverPoint.isVisible = true;
            hoverPoint.relativePosition = new Vector3(
                Mathf.Clamp(nearest.X - 4f, 0f, plotArea.width - hoverPoint.width),
                Mathf.Clamp(nearest.Y - 4f, 0f, plotArea.height - hoverPoint.height));

            tooltipPanel.isVisible = true;
            tooltipLabel.text = BuildPointTooltip(nearest, GetNearestMarkerText(localX));

            float tooltipX = plotArea.relativePosition.x + nearest.X + 10f;
            if (tooltipX + tooltipPanel.width > plotFrame.width - 6f)
            {
                tooltipX = plotArea.relativePosition.x + nearest.X - tooltipPanel.width - 10f;
            }

            float tooltipY = plotArea.relativePosition.y + Mathf.Clamp(nearest.Y - tooltipPanel.height - 8f, 0f, plotArea.height - tooltipPanel.height);
            tooltipPanel.relativePosition = new Vector3(Mathf.Max(4f, tooltipX), Mathf.Max(4f, tooltipY));
        }

        private void OnSelectionMouseUp(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (!isSelecting)
            {
                return;
            }

            isSelecting = false;
            selectionCurrentX = Mathf.Clamp(eventParam.position.x - plotArea.absolutePosition.x, 0f, plotArea.width);
            if (Mathf.Abs(selectionCurrentX - selectionAnchorX) < 8f)
            {
                ClearCustomSelection();
                UpdateGrowth();
                return;
            }

            selectionStartTime = GetTimeForX(Mathf.Min(selectionAnchorX, selectionCurrentX));
            selectionEndTime = GetTimeForX(Mathf.Max(selectionAnchorX, selectionCurrentX));
            hasCustomSelection = selectionEndTime > selectionStartTime;
            RenderSelectionOverlay();
            UpdateGrowth();
        }

        private void OnSelectionMouseLeave(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (!isSelecting)
            {
                HideHover();
            }
        }

        private void HideHover()
        {
            if (hoverLine != null)
            {
                hoverLine.isVisible = false;
            }

            if (hoverPoint != null)
            {
                hoverPoint.isVisible = false;
            }

            if (tooltipPanel != null)
            {
                tooltipPanel.isVisible = false;
            }
        }

        private List<PandemicChartPointSnapshot> FilterPoints(
            IList<PandemicChartPointSnapshot> orderedPoints,
            out DateTime rangeStart,
            out DateTime rangeEnd)
        {
            rangeStart = default(DateTime);
            rangeEnd = default(DateTime);
            if (orderedPoints == null || orderedPoints.Count == 0)
            {
                return new List<PandemicChartPointSnapshot>();
            }

            rangeEnd = orderedPoints[orderedPoints.Count - 1].SimulationTime;
            TimeSpan? selectedDuration = GetSelectedDuration();
            if (!selectedDuration.HasValue)
            {
                rangeStart = orderedPoints[0].SimulationTime;
                return new List<PandemicChartPointSnapshot>(orderedPoints);
            }

            DateTime requestedStart = rangeEnd - selectedDuration.Value;
            PandemicChartPointSnapshot anchor = null;
            PandemicChartPointSnapshot firstInside = null;
            for (int i = 0; i < orderedPoints.Count; i++)
            {
                PandemicChartPointSnapshot point = orderedPoints[i];
                if (point.SimulationTime < requestedStart)
                {
                    anchor = point;
                    continue;
                }

                firstInside = point;
                break;
            }

            rangeStart = requestedStart > orderedPoints[0].SimulationTime
                ? requestedStart
                : orderedPoints[0].SimulationTime;
            DateTime effectiveRangeStart = rangeStart;

            List<PandemicChartPointSnapshot> filtered = orderedPoints
                .Where(point => point.SimulationTime >= effectiveRangeStart)
                .ToList();

            if (anchor != null && firstInside != null && rangeStart > anchor.SimulationTime && rangeStart < firstInside.SimulationTime)
            {
                filtered.Insert(0, new PandemicChartPointSnapshot
                {
                    SimulationTime = rangeStart,
                    InfectedCount = Mathf.RoundToInt(InterpolateValue(anchor, firstInside, rangeStart.Ticks)),
                });
            }

            if (filtered.Count == 0)
            {
                filtered.Add(orderedPoints[orderedPoints.Count - 1]);
                rangeStart = filtered[0].SimulationTime;
            }
            return filtered;
        }

        private static List<PandemicPolicyMarkerSnapshot> FilterMarkers(
            IList<PandemicPolicyMarkerSnapshot> markers,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            if (markers == null || markers.Count == 0)
            {
                return new List<PandemicPolicyMarkerSnapshot>();
            }

            return markers
                .Where(marker => marker.SimulationTime >= rangeStart && marker.SimulationTime <= rangeEnd)
                .OrderBy(marker => marker.SimulationTime)
                .ToList();
        }

        private TimeSpan? GetSelectedDuration()
        {
            switch (selectedRange)
            {
                case PandemicChartTimeRange.Hours24:
                    return TimeSpan.FromHours(24);
                case PandemicChartTimeRange.Days7:
                    return TimeSpan.FromDays(7);
                case PandemicChartTimeRange.Days30:
                    return TimeSpan.FromDays(30);
                default:
                    return null;
            }
        }

        private void ComputeAxisBounds(IList<PandemicChartPointSnapshot> visiblePoints, out float minValue, out float maxValue)
        {
            minValue = 0f;
            maxValue = 10f;
            if (visiblePoints == null || visiblePoints.Count == 0)
            {
                return;
            }

            float rawMin = visiblePoints.Min(point => (float)point.InfectedCount);
            float rawMax = visiblePoints.Max(point => (float)point.InfectedCount);
            float rawRange = Mathf.Max(1f, rawMax - rawMin);

            if (rawMin <= rawMax * 0.12f)
            {
                minValue = 0f;
            }
            else
            {
                minValue = Mathf.Max(0f, rawMin - (rawRange * 0.18f));
            }

            maxValue = rawMax + (rawRange * 0.18f);
            minValue = GetNiceLowerBound(minValue);
            maxValue = GetNiceUpperBound(Mathf.Max(rawMax, maxValue));

            if (maxValue - minValue < 5f)
            {
                maxValue = minValue + 5f;
            }
        }

        private IList<PandemicChartPointSnapshot> GetGrowthPoints()
        {
            if (!hasCustomSelection || currentVisiblePoints == null || currentVisiblePoints.Count == 0)
            {
                return currentVisiblePoints;
            }

            DateTime start = selectionStartTime <= selectionEndTime ? selectionStartTime : selectionEndTime;
            DateTime end = selectionStartTime <= selectionEndTime ? selectionEndTime : selectionStartTime;
            if (end <= start)
            {
                return currentVisiblePoints;
            }

            return BuildSelectionPoints(currentVisiblePoints, start, end);
        }

        private IList<PandemicChartPointSnapshot> BuildSelectionPoints(
            IList<PandemicChartPointSnapshot> source,
            DateTime start,
            DateTime end)
        {
            if (source == null || source.Count == 0)
            {
                return new List<PandemicChartPointSnapshot>();
            }

            var points = new List<PandemicChartPointSnapshot>();
            PandemicChartPointSnapshot previous = null;
            for (int i = 0; i < source.Count; i++)
            {
                PandemicChartPointSnapshot current = source[i];
                if (current.SimulationTime < start)
                {
                    previous = current;
                    continue;
                }

                if (previous != null && previous.SimulationTime < start && current.SimulationTime > start)
                {
                    points.Add(new PandemicChartPointSnapshot
                    {
                        SimulationTime = start,
                        InfectedCount = Mathf.RoundToInt(InterpolateValue(previous, current, start.Ticks)),
                    });
                }

                if (current.SimulationTime >= start && current.SimulationTime <= end)
                {
                    points.Add(current);
                }

                if (current.SimulationTime > end)
                {
                    PandemicChartPointSnapshot beforeEnd = i > 0 ? source[i - 1] : current;
                    if (beforeEnd.SimulationTime < end)
                    {
                        points.Add(new PandemicChartPointSnapshot
                        {
                            SimulationTime = end,
                            InfectedCount = Mathf.RoundToInt(InterpolateValue(beforeEnd, current, end.Ticks)),
                        });
                    }

                    break;
                }

                previous = current;
            }

            if (points.Count == 0)
            {
                points.Add(source[source.Count - 1]);
            }

            return points
                .OrderBy(point => point.SimulationTime)
                .ToList();
        }

        private void NormalizeSelectionToVisibleRange()
        {
            if (!hasCustomSelection)
            {
                return;
            }

            if (currentRangeEnd <= currentRangeStart)
            {
                ClearCustomSelection();
                return;
            }

            DateTime start = selectionStartTime <= selectionEndTime ? selectionStartTime : selectionEndTime;
            DateTime end = selectionStartTime <= selectionEndTime ? selectionEndTime : selectionStartTime;
            if (end < currentRangeStart || start > currentRangeEnd)
            {
                ClearCustomSelection();
                return;
            }

            selectionStartTime = start < currentRangeStart ? currentRangeStart : start;
            selectionEndTime = end > currentRangeEnd ? currentRangeEnd : end;
            if (selectionEndTime <= selectionStartTime)
            {
                ClearCustomSelection();
            }
        }

        private void RenderSelectionOverlay()
        {
            if ((!hasCustomSelection && !isSelecting) || currentRangeEnd <= currentRangeStart)
            {
                ClearSelectionOverlay();
                return;
            }

            float startX = isSelecting ? Mathf.Min(selectionAnchorX, selectionCurrentX) : GetXForTime(selectionStartTime);
            float endX = isSelecting ? Mathf.Max(selectionAnchorX, selectionCurrentX) : GetXForTime(selectionEndTime);
            float left = Mathf.Clamp(Mathf.Min(startX, endX), 0f, plotArea.width);
            float right = Mathf.Clamp(Mathf.Max(startX, endX), 0f, plotArea.width);
            float width = Mathf.Max(2f, right - left);

            selectionBand.isVisible = true;
            selectionBand.relativePosition = new Vector3(left, 0f);
            selectionBand.width = width;
            selectionBand.height = plotArea.height;

            selectionStartLine.isVisible = true;
            selectionStartLine.relativePosition = new Vector3(left, 0f);
            selectionStartLine.height = plotArea.height;

            selectionEndLine.isVisible = true;
            selectionEndLine.relativePosition = new Vector3(right - selectionEndLine.width, 0f);
            selectionEndLine.height = plotArea.height;
        }

        private void ClearSelectionOverlay()
        {
            if (selectionBand != null)
            {
                selectionBand.isVisible = false;
            }

            if (selectionStartLine != null)
            {
                selectionStartLine.isVisible = false;
            }

            if (selectionEndLine != null)
            {
                selectionEndLine.isVisible = false;
            }
        }

        private void ClearCustomSelection()
        {
            isSelecting = false;
            hasCustomSelection = false;
            selectionStartTime = default(DateTime);
            selectionEndTime = default(DateTime);
            ClearSelectionOverlay();
        }

        private float GetXForTime(DateTime time)
        {
            if (currentRangeEnd <= currentRangeStart)
            {
                return 0f;
            }

            double ratio = (time.Ticks - currentRangeStart.Ticks) / (double)(currentRangeEnd.Ticks - currentRangeStart.Ticks);
            return (float)(Mathf.Clamp01((float)ratio) * plotArea.width);
        }

        private DateTime GetTimeForX(float x)
        {
            if (currentRangeEnd <= currentRangeStart)
            {
                return currentRangeStart;
            }

            float clampedX = Mathf.Clamp(x, 0f, plotArea.width);
            double ratio = clampedX / Math.Max(1d, plotArea.width);
            long ticks = currentRangeStart.Ticks + (long)((currentRangeEnd.Ticks - currentRangeStart.Ticks) * ratio);
            return new DateTime(ticks);
        }

        private void UpdateRangeButtons()
        {
            UpdateRangeButton(range24hButton, selectedRange == PandemicChartTimeRange.Hours24);
            UpdateRangeButton(range7dButton, selectedRange == PandemicChartTimeRange.Days7);
            UpdateRangeButton(range30dButton, selectedRange == PandemicChartTimeRange.Days30);
            UpdateRangeButton(rangeAllButton, selectedRange == PandemicChartTimeRange.All);
        }

        private static void UpdateRangeButton(UIButton button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.color = selected ? new Color32(48, 132, 204, 255) : new Color32(78, 78, 78, 255);
        }

        private UIButton CreateRangeButton(string text, float x, float y, float width)
        {
            UIButton button = root.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.width = width;
            button.height = 22f;
            button.relativePosition = new Vector3(x, y);
            button.text = text;
            button.textScale = 0.66f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textHorizontalAlignment = UIHorizontalAlignment.Center;
            return button;
        }

        private static UIPanel CreateSolidPanel(UIComponent parent, Color32 color)
        {
            UIPanel panel = parent.AddUIComponent<UIPanel>();
            panel.autoSize = false;
            panel.backgroundSprite = "EmptySprite";
            panel.color = color;
            panel.opacity = 1f;
            panel.isVisible = false;
            return panel;
        }

        private void EnsureSeriesWidgets(int sliceCount, int pointCount)
        {
            while (fillSlices.Count < sliceCount)
            {
                UIPanel fillSlice = CreateSolidPanel(plotArea, FillColor);
                fillSlice.zOrder = 0;
                fillSlices.Add(fillSlice);

                UIPanel lineSlice = CreateSolidPanel(plotArea, LineColor);
                lineSlice.zOrder = 1;
                lineSlices.Add(lineSlice);
            }

            int pointMarkersNeeded = Math.Max(4, Math.Min(pointCount, 12));
            while (pointMarkers.Count < pointMarkersNeeded)
            {
                UISprite point = plotArea.AddUIComponent<UISprite>();
                point.autoSize = false;
                point.width = 6f;
                point.height = 6f;
                point.spriteName = "EmptySprite";
                point.color = PointColor;
                point.isVisible = false;
                pointMarkers.Add(point);
            }
        }

        private void EnsurePolicyWidgets(int markerCount)
        {
            while (policyLines.Count < markerCount)
            {
                UIPanel line = CreateSolidPanel(plotArea, AxisColor);
                line.zOrder = 3;
                policyLines.Add(line);

                UILabel label = plotFrame.AddUIComponent<UILabel>();
                label.autoSize = false;
                label.width = 18f;
                label.height = 12f;
                label.textScale = 0.58f;
                label.textAlignment = UIHorizontalAlignment.Center;
                label.isVisible = false;
                policyLabels.Add(label);
            }
        }

        private void EnsureYAxisWidgets(int count)
        {
            while (yAxisGridLines.Count < count)
            {
                UIPanel line = CreateSolidPanel(plotArea, GridColor);
                line.zOrder = 0;
                yAxisGridLines.Add(line);

                UILabel label = plotFrame.AddUIComponent<UILabel>();
                label.autoSize = false;
                label.width = PlotLeftMargin - 6f;
                label.height = 14f;
                label.textScale = 0.6f;
                label.textAlignment = UIHorizontalAlignment.Right;
                label.textColor = new Color32(220, 220, 220, 255);
                label.isVisible = false;
                yAxisLabels.Add(label);
            }
        }

        private void EnsureXAxisWidgets(int count)
        {
            while (xAxisGridLines.Count < count)
            {
                UIPanel line = CreateSolidPanel(plotArea, GridColor);
                line.zOrder = 0;
                xAxisGridLines.Add(line);

                UILabel label = plotFrame.AddUIComponent<UILabel>();
                label.autoSize = false;
                label.width = 56f;
                label.height = 14f;
                label.textScale = 0.58f;
                label.textAlignment = UIHorizontalAlignment.Center;
                label.textColor = new Color32(220, 220, 220, 255);
                label.isVisible = false;
                xAxisLabels.Add(label);
            }
        }

        private void HideSeriesWidgets()
        {
            foreach (UIPanel slice in fillSlices)
            {
                slice.isVisible = false;
            }

            foreach (UIPanel slice in lineSlices)
            {
                slice.isVisible = false;
            }

            foreach (UISprite point in pointMarkers)
            {
                point.isVisible = false;
            }

            if (xAxisLine != null)
            {
                xAxisLine.isVisible = false;
            }

            if (yAxisLine != null)
            {
                yAxisLine.isVisible = false;
            }
        }

        private void HidePolicyWidgets()
        {
            foreach (UIPanel line in policyLines)
            {
                line.isVisible = false;
            }

            foreach (UILabel label in policyLabels)
            {
                label.isVisible = false;
            }
        }

        private void HideAxisWidgets()
        {
            foreach (UIPanel line in yAxisGridLines)
            {
                line.isVisible = false;
            }

            foreach (UILabel label in yAxisLabels)
            {
                label.isVisible = false;
            }

            foreach (UIPanel line in xAxisGridLines)
            {
                line.isVisible = false;
            }

            foreach (UILabel label in xAxisLabels)
            {
                label.isVisible = false;
            }
        }

        private float GetYPosition(float value, float minValue, float maxValue)
        {
            if (maxValue <= minValue)
            {
                return plotArea.height;
            }

            float ratio = Mathf.Clamp01((value - minValue) / (maxValue - minValue));
            return plotArea.height - (ratio * plotArea.height);
        }

        private float InterpolateValue(PandemicChartPointSnapshot startPoint, PandemicChartPointSnapshot endPoint, long ticks)
        {
            if (startPoint == null)
            {
                return 0f;
            }

            if (endPoint == null || endPoint.SimulationTime <= startPoint.SimulationTime)
            {
                return startPoint.InfectedCount;
            }

            double total = endPoint.SimulationTime.Ticks - startPoint.SimulationTime.Ticks;
            if (total <= 0d)
            {
                return startPoint.InfectedCount;
            }

            float ratio = (float)((ticks - startPoint.SimulationTime.Ticks) / total);
            return Mathf.Lerp(startPoint.InfectedCount, endPoint.InfectedCount, Mathf.Clamp01(ratio));
        }

        private string BuildPointTooltip(RenderedChartPoint point)
        {
            return BuildPointTooltip(point, null);
        }

        private string BuildPointTooltip(RenderedChartPoint point, string markerText)
        {
            string tooltip = point.Point.SimulationTime.ToString("g", cultureInfo)
                + "\nInfected: " + point.Point.InfectedCount.ToString("N0", cultureInfo)
                + " | Delta: " + FormatSigned(point.Delta);

            if (!string.IsNullOrEmpty(markerText))
            {
                tooltip += "\n" + markerText;
            }

            return tooltip;
        }

        private string GetNearestMarkerText(float localX)
        {
            if (renderedMarkers.Count == 0)
            {
                return null;
            }

            RenderedPolicyMarker nearest = renderedMarkers[0];
            float nearestDistance = Mathf.Abs(nearest.X - localX);
            for (int i = 1; i < renderedMarkers.Count; i++)
            {
                float distance = Mathf.Abs(renderedMarkers[i].X - localX);
                if (distance < nearestDistance)
                {
                    nearest = renderedMarkers[i];
                    nearestDistance = distance;
                }
            }

            if (nearestDistance > HoverThreshold)
            {
                return null;
            }

            string action = nearest.Marker.Enabled ? "enabled" : "disabled";
            string policy = nearest.Marker.Type == PandemicPolicyMarkerType.Masks ? "Masks" : "Lockdown";
            return policy + " " + action + " (" + nearest.Marker.SimulationTime.ToString("g", cultureInfo) + ")";
        }

        private string FormatXAxisLabel(DateTime value, TimeSpan visibleSpan)
        {
            if (visibleSpan <= TimeSpan.FromDays(1))
            {
                return value.ToString("HH:mm", cultureInfo);
            }

            if (visibleSpan <= TimeSpan.FromDays(31))
            {
                return value.ToString("dd MMM", cultureInfo);
            }

            if (visibleSpan <= TimeSpan.FromDays(366))
            {
                return value.ToString("MMM yyyy", cultureInfo);
            }

            return value.ToString("yyyy", cultureInfo);
        }

        private static float GetNiceUpperBound(float maxValue)
        {
            float raw = Mathf.Max(10f, maxValue * 1.1f);
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            float normalized = raw / magnitude;
            float niceNormalized;
            if (normalized <= 1f)
            {
                niceNormalized = 1f;
            }
            else if (normalized <= 2f)
            {
                niceNormalized = 2f;
            }
            else if (normalized <= 5f)
            {
                niceNormalized = 5f;
            }
            else
            {
                niceNormalized = 10f;
            }

            return niceNormalized * magnitude;
        }

        private static float GetNiceLowerBound(float minValue)
        {
            if (minValue <= 0f)
            {
                return 0f;
            }

            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(minValue)));
            float normalized = minValue / magnitude;
            float niceNormalized;
            if (normalized >= 10f)
            {
                niceNormalized = 10f;
            }
            else if (normalized >= 5f)
            {
                niceNormalized = 5f;
            }
            else if (normalized >= 2f)
            {
                niceNormalized = 2f;
            }
            else
            {
                niceNormalized = 1f;
            }

            return Mathf.Floor(minValue / (niceNormalized * magnitude)) * niceNormalized * magnitude;
        }

        private static float GetNiceStep(float rawStep)
        {
            float safeStep = Mathf.Max(1f, rawStep);
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(safeStep)));
            float normalized = safeStep / magnitude;
            float niceNormalized;
            if (normalized <= 1f)
            {
                niceNormalized = 1f;
            }
            else if (normalized <= 2f)
            {
                niceNormalized = 2f;
            }
            else if (normalized <= 5f)
            {
                niceNormalized = 5f;
            }
            else
            {
                niceNormalized = 10f;
            }

            return niceNormalized * magnitude;
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
        }

        private static Color32 GetMarkerColor(PandemicPolicyMarkerSnapshot marker)
        {
            if (marker == null)
            {
                return new Color32(180, 180, 180, 255);
            }

            switch (marker.Type)
            {
                case PandemicPolicyMarkerType.Masks:
                    return marker.Enabled ? new Color32(48, 172, 212, 255) : new Color32(20, 118, 168, 255);
                case PandemicPolicyMarkerType.Lockdown:
                    return marker.Enabled ? new Color32(228, 124, 36, 255) : new Color32(186, 66, 32, 255);
                default:
                    return new Color32(180, 180, 180, 255);
            }
        }

        private enum PandemicChartTimeRange
        {
            Hours24,
            Days7,
            Days30,
            All,
        }

        private sealed class RenderedChartPoint
        {
            public PandemicChartPointSnapshot Point { get; set; }

            public float X { get; set; }

            public float Y { get; set; }

            public int Delta { get; set; }
        }

        private sealed class RenderedPolicyMarker
        {
            public PandemicPolicyMarkerSnapshot Marker { get; set; }

            public float X { get; set; }
        }
    }
}
