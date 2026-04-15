namespace RealTime.UI
{
    using System;
    using System.Globalization;
    using ColossalFramework.UI;
    using RealTime.Pandemic;
    using UnityEngine;

    internal sealed class PandemicSidrBarView
    {
        private static readonly Color32 SusceptibleColor = new Color32(102, 136, 176, 255);
        private static readonly Color32 InfectedColor = new Color32(220, 96, 74, 255);
        private static readonly Color32 RecoveredColor = new Color32(76, 178, 108, 255);
        private static readonly Color32 DeadColor = new Color32(132, 76, 148, 255);
        private static readonly string[] Labels = { "S", "I", "R", "D" };

        private readonly Color32[] colors =
        {
            SusceptibleColor,
            InfectedColor,
            RecoveredColor,
            DeadColor,
        };

        private UIPanel root;
        private UILabel titleLabel;
        private UILabel summaryLabel;
        private UILabel stackedLabel;
        private UIPanel stackedTrack;
        private UIPanel[] stackedSegments;
        private UILabel barTitleLabel;
        private UIPanel[] barTracks;
        private UIPanel[] barFills;
        private UILabel[] barLabels;
        private UILabel[] barValues;
        private CultureInfo cultureInfo = CultureInfo.CurrentCulture;

        public UIPanel Component => root;

        public void Initialize(UIComponent parent, float width, float height)
        {
            root = parent.AddUIComponent<UIPanel>();
            root.autoSize = false;
            root.width = width;
            root.height = height;
            root.backgroundSprite = "MenuPanel2";
            root.opacity = 0.6f;
            root.clipChildren = true;

            titleLabel = root.AddUIComponent<UILabel>();
            titleLabel.autoSize = false;
            titleLabel.width = width - 12f;
            titleLabel.height = 18f;
            titleLabel.relativePosition = new Vector3(6f, 4f);
            titleLabel.text = "Tracked cohort SIDR";
            titleLabel.textScale = 0.72f;
            titleLabel.textColor = new Color32(245, 245, 245, 255);

            summaryLabel = root.AddUIComponent<UILabel>();
            summaryLabel.autoSize = false;
            summaryLabel.width = width - 12f;
            summaryLabel.height = 14f;
            summaryLabel.relativePosition = new Vector3(6f, 22f);
            summaryLabel.textScale = 0.58f;
            summaryLabel.textColor = new Color32(228, 228, 228, 255);

            stackedLabel = root.AddUIComponent<UILabel>();
            stackedLabel.autoSize = false;
            stackedLabel.width = width - 12f;
            stackedLabel.height = 14f;
            stackedLabel.relativePosition = new Vector3(6f, 42f);
            stackedLabel.text = "Share of tracked cohort";
            stackedLabel.textScale = 0.6f;
            stackedLabel.textColor = new Color32(228, 228, 228, 255);

            stackedTrack = root.AddUIComponent<UIPanel>();
            stackedTrack.autoSize = false;
            stackedTrack.width = width - 12f;
            stackedTrack.height = 22f;
            stackedTrack.relativePosition = new Vector3(6f, 58f);
            stackedTrack.backgroundSprite = "GenericPanel";
            stackedTrack.color = new Color32(32, 32, 32, 180);
            stackedTrack.clipChildren = true;

            stackedSegments = new UIPanel[4];
            for (int i = 0; i < stackedSegments.Length; i++)
            {
                UIPanel segment = stackedTrack.AddUIComponent<UIPanel>();
                segment.autoSize = false;
                segment.height = stackedTrack.height;
                segment.backgroundSprite = "EmptySprite";
                segment.color = colors[i];
                segment.opacity = 1f;
                segment.isInteractive = true;
                stackedSegments[i] = segment;
            }

            barTitleLabel = root.AddUIComponent<UILabel>();
            barTitleLabel.autoSize = false;
            barTitleLabel.width = width - 12f;
            barTitleLabel.height = 14f;
            barTitleLabel.relativePosition = new Vector3(6f, 88f);
            barTitleLabel.text = "Tracked cohort shares";
            barTitleLabel.textScale = 0.6f;
            barTitleLabel.textColor = new Color32(228, 228, 228, 255);

            float barAreaTop = 106f;
            float barTrackHeight = 52f;
            float slotWidth = (width - 18f) / 4f;
            barTracks = new UIPanel[4];
            barFills = new UIPanel[4];
            barLabels = new UILabel[4];
            barValues = new UILabel[4];
            for (int i = 0; i < 4; i++)
            {
                float x = 6f + (slotWidth * i);
                UILabel barValue = root.AddUIComponent<UILabel>();
                barValue.autoSize = false;
                barValue.width = slotWidth - 4f;
                barValue.height = 12f;
                barValue.relativePosition = new Vector3(x + 2f, barAreaTop);
                barValue.textScale = 0.54f;
                barValue.textAlignment = UIHorizontalAlignment.Center;
                barValue.textColor = new Color32(240, 240, 240, 255);
                barValues[i] = barValue;

                UIPanel track = root.AddUIComponent<UIPanel>();
                track.autoSize = false;
                track.width = Mathf.Max(18f, slotWidth - 10f);
                track.height = barTrackHeight;
                track.relativePosition = new Vector3(x + ((slotWidth - track.width) * 0.5f), barAreaTop + 14f);
                track.backgroundSprite = "GenericPanel";
                track.color = new Color32(28, 28, 28, 180);
                track.clipChildren = true;
                track.isInteractive = true;
                barTracks[i] = track;

                UIPanel fill = track.AddUIComponent<UIPanel>();
                fill.autoSize = false;
                fill.width = track.width;
                fill.backgroundSprite = "EmptySprite";
                fill.color = colors[i];
                fill.opacity = 1f;
                barFills[i] = fill;

                UILabel barLabel = root.AddUIComponent<UILabel>();
                barLabel.autoSize = false;
                barLabel.width = slotWidth - 4f;
                barLabel.height = 12f;
                barLabel.relativePosition = new Vector3(x + 2f, barAreaTop + 14f + barTrackHeight + 4f);
                barLabel.text = Labels[i];
                barLabel.textScale = 0.58f;
                barLabel.textAlignment = UIHorizontalAlignment.Center;
                barLabel.textColor = new Color32(235, 235, 235, 255);
                barLabels[i] = barLabel;
            }
        }

        public void Refresh(PandemicLiveSnapshot snapshot, CultureInfo culture)
        {
            cultureInfo = culture ?? CultureInfo.CurrentCulture;
            int susceptible = snapshot?.Healthy ?? 0;
            int infected = snapshot?.Sick ?? 0;
            int recovered = snapshot?.Recovered ?? 0;
            int dead = snapshot?.Dead ?? 0;
            int total = Math.Max(0, snapshot?.TrackedPopulation ?? (susceptible + infected + recovered + dead));
            int[] counts = { susceptible, infected, recovered, dead };

            summaryLabel.text = total > 0
                ? "Tracked cohort " + total.ToString("N0", cultureInfo)
                : "No tracked cohort data";

            float stackedX = 0f;
            for (int i = 0; i < counts.Length; i++)
            {
                float percent = total > 0 ? (counts[i] * 100f) / total : 0f;
                float width = total > 0 ? Mathf.Round((counts[i] / (float)total) * stackedTrack.width) : 0f;
                UIPanel segment = stackedSegments[i];
                segment.relativePosition = new Vector3(stackedX, 0f);
                segment.width = i == counts.Length - 1 ? Mathf.Max(0f, stackedTrack.width - stackedX) : Mathf.Max(0f, width);
                segment.isVisible = counts[i] > 0;
                segment.tooltip = BuildTooltip(Labels[i], counts[i], percent);
                stackedX += segment.width;

                UIPanel track = barTracks[i];
                UIPanel fill = barFills[i];
                UILabel valueLabel = barValues[i];
                float barHeight = total > 0 ? Mathf.Round((counts[i] / (float)total) * track.height) : 0f;
                fill.height = Mathf.Clamp(barHeight, 0f, track.height);
                fill.relativePosition = new Vector3(0f, track.height - fill.height);
                track.tooltip = BuildTooltip(Labels[i], counts[i], percent);
                valueLabel.text = percent.ToString("0.0", cultureInfo) + "%";
            }
        }

        private string BuildTooltip(string label, int count, float percent)
        {
            return label
                + "\nCount: " + count.ToString("N0", cultureInfo)
                + "\nShare of tracked cohort: " + percent.ToString("0.0", cultureInfo) + "%";
        }
    }
}
