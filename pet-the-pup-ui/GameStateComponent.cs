﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.dotStart.PetThePup.UI {
  public class GameStateComponent : IComponent {
    private const float Spacing = 5;
    
    public string ComponentName => "Pet the Pup";

    /// <inheritdoc />
    public float HorizontalWidth {
      get {
        if (!this._sizePopulated) {
          return this.MinimumWidth;
        }
        
        List<SimpleLabel> labels = this.VisibleLabels;
        float width = 0;
        float cellWidth = 0;

        for (int i = 0; i < labels.Count; ++i) {
          if (i != 0 && i % 2 == 0) {
            width += Spacing;
            width += cellWidth;
            cellWidth = 0;
          }

          cellWidth = Math.Max(cellWidth, labels[i].ActualWidth);
        }

        return Math.Max(this.MinimumWidth, width) + this.PaddingLeft + this.PaddingRight;
      }
    }

    /// <inheritdoc />
    public float VerticalHeight {
      get {
        if (!this._sizePopulated) {
          return this.MinimumHeight;
        }
        
        float height = 0;
        List<SimpleLabel> labels = this.VisibleLabels;

        for (int i = 0; i < labels.Count; i += 2) {
          if (i != 0) {
            height += Spacing;
          }

          height += labels[i].Height;
        }

        if (this.Settings.DisplayRemainingPups) {
          if (labels.Count != 0) {
            height += Spacing;
          }

          foreach (SimpleLabel label in this._remainingPupLabels) {
            height += Spacing;
            height += label.Height;
          }
        }

        if (this.Settings.DisplayPetStatistics) {
          if (labels.Count != 0 || this.Settings.DisplayRemainingPups) {
            height += Spacing;
          }

          height += this._petStatisticsLabel.Height;

          foreach (SimpleLabel label in this._petCountLabels) {
            height += Spacing;
            height += label.Height;
          }
        }

        return height + this.PaddingTop + this.PaddingBottom;
      }
    }

    public float MinimumHeight => 25;
    public float MinimumWidth => 30;
    public float PaddingTop => 7f;
    public float PaddingBottom => 7f;
    public float PaddingLeft => 7f;
    public float PaddingRight => 7f;

    public IDictionary<string, Action> ContextMenuControls { get; }

    public readonly GameStateSettings Settings = new GameStateSettings();
    private readonly GameMemoryImpl _memory = GameMemoryImpl.Instance;
    private readonly PuppyGallery _registry = PuppyGallery.Instance;

    private readonly GraphicsCache _cache = new GraphicsCache();
    private readonly SimpleLabel _totalPupsPetNameLabel = new SimpleLabel("Total Pups:");
    private readonly SimpleLabel _totalPupsPetValueLabel = new SimpleLabel("0 (0)");
    private readonly SimpleLabel _uniquePupsPetNameLabel = new SimpleLabel("Unique Pups Discovered:");
    private readonly SimpleLabel _uniquePupsPetValueLabel = new SimpleLabel("0");
    private readonly SimpleLabel _totalConversationsNameLabel = new SimpleLabel("Total Conversations:");
    private readonly SimpleLabel _totalConversationsValueLabel = new SimpleLabel("0");
    private readonly SimpleLabel _lastUniquePupNameLabel = new SimpleLabel("Last Pup:");
    private readonly SimpleLabel _lastUniquePupValueLabel = new SimpleLabel("-");
    private readonly SimpleLabel _remainingPupNameLabel = new SimpleLabel("Remaining Unique Pups:");
    private readonly SimpleLabel _remainingPupValueLabel = new SimpleLabel("-");
    private readonly SimpleLabel _petStatisticsLabel = new SimpleLabel("Pet Statistics:");

    /// <summary>
    /// Exposes a list of information labels visible within the component.
    /// 
    /// Note that this does not contain the list of remaining pups as we cannot automatically
    /// position it at the moment (nor will it show up in a horizontal orientation).
    /// </summary>
    private List<SimpleLabel> VisibleLabels {
      get {
        List<SimpleLabel> labels = new List<SimpleLabel>();

        if (this.Settings.DisplayTotalPups) {
          labels.Add(this._totalPupsPetNameLabel);
          labels.Add(this._totalPupsPetValueLabel);
        }

        if (this.Settings.DisplayUniquePups) {
          labels.Add(this._uniquePupsPetNameLabel);
          labels.Add(this._uniquePupsPetValueLabel);
        }

        if (this.Settings.DisplayTotalConversations) {
          labels.Add(this._totalConversationsNameLabel);
          labels.Add(this._totalConversationsValueLabel);
        }

        if (this.Settings.DisplayLastDiscoveredPup) {
          labels.Add(this._lastUniquePupNameLabel);
          labels.Add(this._lastUniquePupValueLabel);
        }

        if (this.Settings.DisplayRemainingPups) {
          labels.Add(this._remainingPupNameLabel);
          labels.Add(this._remainingPupValueLabel);
        }

        return labels;
      }
    }

    private bool _sizePopulated;
    private SimpleLabel[] _remainingPupLabels = new SimpleLabel[0];
    private SimpleLabel[] _petCountLabels = new SimpleLabel[0];

    public GameStateComponent() {
      this.ContextMenuControls = new Dictionary<string, Action>();

      // Initialize width and heights to prevent overflow issues caused by variable defaults
      this._totalPupsPetNameLabel.Width = 5;
      this._totalPupsPetNameLabel.Height = 5;
      this._totalPupsPetValueLabel.Width = 5;
      this._totalPupsPetValueLabel.Height = 5;

      this._uniquePupsPetNameLabel.Width = 5;
      this._uniquePupsPetNameLabel.Height = 5;
      this._uniquePupsPetValueLabel.Width = 5;
      this._uniquePupsPetValueLabel.Height = 5;

      this._totalConversationsNameLabel.Width = 5;
      this._totalConversationsNameLabel.Height = 5;
      this._totalConversationsValueLabel.Width = 5;
      this._totalConversationsValueLabel.Height = 5;

      this._lastUniquePupNameLabel.Width = 5;
      this._lastUniquePupNameLabel.Height = 5;
      this._lastUniquePupValueLabel.Width = 5;
      this._lastUniquePupValueLabel.Height = 5;

      this._remainingPupNameLabel.Width = 5;
      this._remainingPupNameLabel.Height = 5;
      this._remainingPupValueLabel.Width = 5;
      this._remainingPupValueLabel.Height = 5;

      this._petStatisticsLabel.Width = 5;
      this._petStatisticsLabel.Height = 5;
      
      // claim the registry instance to be sure
      this._registry.Claim();
      
      // start memory checking (if not already running)
      this._memory.Start();
    }

    /// <summary>
    /// Updates the display properties of all visible labels.
    /// </summary>
    /// <param name="g"></param>
    /// <param name="state"></param>
    private void UpdateDisplaySettings(Graphics g, LiveSplitState state, LayoutMode mode) {
      List<SimpleLabel> labels = this.VisibleLabels;

      float fontHeight = state.LayoutSettings.TextFont.Height;
      float xOffset = this.PaddingLeft;
      float yOffset = this.PaddingTop;

      for (int i = 0; i < labels.Count / 2; ++i) {
        SimpleLabel name = labels[i * 2];
        SimpleLabel value = labels[i * 2 + 1];

        UpdateLabelStyle(g, state, name);
        UpdateLabelStyle(g, state, value);

        switch (mode) {
          case LayoutMode.Horizontal:
            name.X = xOffset;
            name.Y = this.PaddingTop;
            value.X = xOffset;
            value.Y = fontHeight + Spacing;

            xOffset += Math.Max(name.ActualWidth, value.ActualWidth) + 10;
            break;
          case LayoutMode.Vertical:
            name.X = this.PaddingLeft;
            name.Y = yOffset;
            value.X = name.ActualWidth + Spacing;
            value.Y = yOffset;

            yOffset += name.Height + Spacing;
            break;
          default:
            throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
      }

      if (mode == LayoutMode.Vertical) {
        if (this.Settings.DisplayRemainingPups) {
          foreach (SimpleLabel label in this._remainingPupLabels) {
            UpdateLabelStyle(g, state, label);

            label.X = this.PaddingLeft * 2;
            label.Y = yOffset;

            yOffset += label.Height + Spacing;
          }
        }

        if (this.Settings.DisplayPetStatistics) {
          UpdateLabelStyle(g, state, this._petStatisticsLabel);
          this._petStatisticsLabel.X = this.PaddingLeft;
          this._petStatisticsLabel.Y = yOffset;

          yOffset += this._petStatisticsLabel.Height + Spacing;

          foreach (SimpleLabel label in this._petCountLabels) {
            UpdateLabelStyle(g, state, label);

            label.X = this.PaddingLeft * 2;
            label.Y = yOffset;

            yOffset += label.Height + Spacing;
          }
        }
      }

      this._sizePopulated = true;
    }

    /// <summary>
    /// Updates the style of whatever label is passed to match the rest of the elements within the
    /// application UI.
    /// </summary>
    /// <param name="g"></param>
    /// <param name="state"></param>
    /// <param name="label"></param>
    private static void UpdateLabelStyle(Graphics g, LiveSplitState state, SimpleLabel label) {
      label.ForeColor = state.LayoutSettings.TextColor;
      label.ShadowColor = state.LayoutSettings.ShadowsColor;
      label.OutlineColor = state.LayoutSettings.TextOutlineColor;
      label.Font = state.LayoutSettings.TextFont;
      
      label.SetActualWidth(g);
      label.Height = label.Font.Height;
      label.Width = g.MeasureString(label.Text, label.Font).Width;
    }

    /// <summary>
    /// Draws all labels within the component.
    /// </summary>
    /// <param name="g"></param>
    /// <param name="mode"></param>
    private void Draw(Graphics g, LayoutMode mode) {
      List<SimpleLabel> labels = this.VisibleLabels;

      foreach (SimpleLabel label in labels) {
        label.Draw(g);
      }

      if (mode == LayoutMode.Vertical) {
        if (this.Settings.DisplayRemainingPups) {
          foreach (SimpleLabel label in this._remainingPupLabels) {
            label.Draw(g);
          }
        }

        if (this.Settings.DisplayPetStatistics) {
          this._petStatisticsLabel.Draw(g);

          foreach (SimpleLabel label in this._petCountLabels) {
            label.Draw(g);
          }
        }
      }
    }
    
    /// <inheritdoc />
    public void Dispose() {
      this._registry.Dispose();
      this._memory.Dispose();
    }

    /// <inheritdoc />
    public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion) {
      this.UpdateDisplaySettings(g, state, LayoutMode.Horizontal);
      this.Draw(g, LayoutMode.Horizontal);
    }

    /// <inheritdoc />
    public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion) {
      this.UpdateDisplaySettings(g, state, LayoutMode.Vertical);
      this.Draw(g, LayoutMode.Vertical);
    }

    /// <inheritdoc />
    public Control GetSettingsControl(LayoutMode mode) {
      return this.Settings;
    }

    /// <inheritdoc />
    public XmlNode GetSettings(XmlDocument document) {
      return this.Settings.GetSettings(document);
    }

    /// <inheritdoc />
    public void SetSettings(XmlNode settings) {
      this.Settings.SetSettings(settings);
    }

    private void ResizeArray<T>(uint target, ref T[] array, Func<T> initializer) {
      if (array.Length == target) {
        return;
      }
      
      T[] newArray = new T[target];

      if (target != 0) {
        Array.Copy(array, 0, array, 0, Math.Min(array.Length, target));
      }

      if (target > array.Length) {
        for (int i = array.Length; i < target; ++i) {
          newArray[i] = initializer();
        }
      }

      array = newArray;
    }

    /// <inheritdoc />
    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height,
      LayoutMode mode) {
      // re-initialize list of remaining pups if necessary
      this.ResizeArray(this.Settings.RemainingPupAmount, ref this._remainingPupLabels,
        () => new SimpleLabel("-") {
          Width = 5,
          Height = 5
        });

      this.ResizeArray(this.Settings.PetStatisticsAmount, ref this._petCountLabels,
        () => new SimpleLabel("-") {
          Width = 5,
          Height = 5
        });
      
      // reset all values within our graphics cache in order to verify whether we need to re-draw
      // the entire component (e.g. when values change)
      this._cache.Restart();

      this._cache["TotalPupsPet"] = this._memory.TotalPupCount;
      this._cache["SessionPupsPet"] = this._memory.PupCount;
      this._cache["UniquePupsPet"] = this._registry.Discovered;
      this._cache["TotalConversations"] = this._memory.ConversationCount;
      this._cache["LastUniquePup"] = this._registry.Discovered != 0 ? this._registry.Latest.ToString() : "-";
      
      ReadOnlyCollection<Puppy> knownRemaining = this._registry.Remaining;
      ReadOnlyDictionary<Puppy, uint> statistics = this._registry.PetCount;

      this._cache["RemainingPupCount"] = knownRemaining.Count;
      this._cache["RemainingPups"] = knownRemaining;
      this._cache["PetStatisticsCount"] = statistics;

      if (invalidator != null && this._cache.HasChanged) {
        this._totalPupsPetValueLabel.Text = this._memory.TotalPupCount + " (" + this._memory.PupCount + ")";
        this._uniquePupsPetValueLabel.Text = this._registry.Discovered.ToString();
        this._totalConversationsValueLabel.Text = this._memory.ConversationCount.ToString();
        this._lastUniquePupValueLabel.Text = this._registry.Discovered != 0 ? this._registry.Latest.ToString() : "-";
        this._remainingPupValueLabel.Text = knownRemaining.Count.ToString();

        for (int i = 0; i < this._remainingPupLabels.Length; ++i) {
          this._remainingPupLabels[i].Text =
            i < knownRemaining.Count ? "- " + knownRemaining[i] : "-";
        }

        List<KeyValuePair<Puppy, uint>> statisticEntries = statistics.ToList();
        statisticEntries.Sort((a, b) => -1 * a.Value.CompareTo(b.Value));

        for (int i = 0; i < this._petCountLabels.Length; ++i) {
          if (i >= statisticEntries.Count) {
            this._petCountLabels[i].Text = "-";
            continue;
          }
          
          this._petCountLabels[i].Text = statisticEntries[i].Key + ": " + statisticEntries[i].Value;
        }
        
        invalidator.Invalidate(0, 0, width, height);
      }
    }
  }
}
