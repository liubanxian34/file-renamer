using System;
using System.Windows;
using System.Windows.Controls;

namespace Renamer
{
    public partial class RuleWindow : Window
    {
        private readonly Rule _target;
        private readonly Rule _draft;

        public RuleWindow(Rule target)
        {
            InitializeComponent();
            _target = target;
            _draft = target.Clone();
            TitleText.Text = "编辑规则 · " + _draft.Title;
            Title = TitleText.Text;

            ShowBox(_draft.Kind);
            LoadUi(_draft.Kind);
            UpdateSepPanel();
        }

        private void ShowBox(RuleKind kind)
        {
            BoxReplace.Visibility = kind == RuleKind.Replace ? Visibility.Visible : Visibility.Collapsed;
            BoxInsert.Visibility = kind == RuleKind.Insert ? Visibility.Visible : Visibility.Collapsed;
            BoxDelete.Visibility = kind == RuleKind.Delete ? Visibility.Visible : Visibility.Collapsed;
            BoxNumber.Visibility = kind == RuleKind.Number ? Visibility.Visible : Visibility.Collapsed;
            BoxDate.Visibility = kind == RuleKind.Date ? Visibility.Visible : Visibility.Collapsed;
            BoxCase.Visibility = kind == RuleKind.Case ? Visibility.Visible : Visibility.Collapsed;
            BoxClean.Visibility = kind == RuleKind.Clean ? Visibility.Visible : Visibility.Collapsed;
            BoxExt.Visibility = kind == RuleKind.Ext ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadUi(RuleKind kind)
        {
            switch (kind)
            {
                case RuleKind.Replace:
                    FindBox.Text = _draft.Find;
                    ReplaceBox.Text = _draft.Replace;
                    RegexBox.IsChecked = _draft.RegexOn;
                    IgnoreCaseBox.IsChecked = _draft.IgnoreCase;
                    NameOnlyBox.IsChecked = _draft.NameOnly;
                    break;
                case RuleKind.Insert:
                    InsertText.Text = _draft.Text;
                    InsertAtBox.SelectedIndex = (int)_draft.At;
                    InsertPos.Text = _draft.Pos.ToString();
                    break;
                case RuleKind.Delete:
                    BySepBox.IsChecked = _draft.BySeparator;
                    SepBox.Text = _draft.Separator;
                    SegBox.Text = _draft.Segment.ToString();
                    DelFrom.Text = _draft.From.ToString();
                    DelCount.Text = _draft.Count.ToString();
                    break;
                case RuleKind.Number:
                    NumStart.Text = _draft.Start.ToString();
                    NumStep.Text = _draft.Step.ToString();
                    NumDigits.Text = _draft.Digits.ToString();
                    NumAtBox.SelectedIndex = (int)_draft.NumAt;
                    NumPos.Text = _draft.Pos.ToString();
                    break;
                case RuleKind.Date:
                    DateFromBox.SelectedIndex = (int)_draft.DateFrom;
                    DateFmt.Text = _draft.DateFormat;
                    DateAtBox.SelectedIndex = _draft.DateAt == InsertAt.End ? 1 : 0;
                    break;
                case RuleKind.Case:
                    CaseBox.SelectedIndex = (int)_draft.Case;
                    break;
                case RuleKind.Clean:
                    TrimBox.IsChecked = _draft.TrimSpace;
                    CollapseBox.IsChecked = _draft.CollapseSpace;
                    BracketBox.IsChecked = _draft.RemoveBrackets;
                    SpecialBox.IsChecked = _draft.RemoveSpecial;
                    WidthBox.IsChecked = _draft.FullWidth;
                    break;
                case RuleKind.Ext:
                    ExtBox.Text = _draft.NewExt;
                    break;
            }
        }

        private void BySepChanged(object sender, RoutedEventArgs e)
        {
            UpdateSepPanel();
        }

        private void UpdateSepPanel()
        {
            bool sep = BySepBox.IsChecked == true;
            if (SepPanel != null) SepPanel.Visibility = sep ? Visibility.Visible : Visibility.Collapsed;
            if (PosPanel != null) PosPanel.Visibility = sep ? Visibility.Collapsed : Visibility.Visible;
        }

        private static int I(string s, int fallback)
        {
            int v;
            return int.TryParse((s ?? "").Trim(), out v) ? v : fallback;
        }

        private void Ok(object sender, RoutedEventArgs e)
        {
            switch (_draft.Kind)
            {
                case RuleKind.Replace:
                    _draft.Find = FindBox.Text;
                    _draft.Replace = ReplaceBox.Text;
                    _draft.RegexOn = RegexBox.IsChecked == true;
                    _draft.IgnoreCase = IgnoreCaseBox.IsChecked == true;
                    _draft.NameOnly = NameOnlyBox.IsChecked == true;
                    break;
                case RuleKind.Insert:
                    _draft.Text = InsertText.Text;
                    _draft.At = (InsertAt)Math.Max(0, InsertAtBox.SelectedIndex);
                    _draft.Pos = I(InsertPos.Text, 0);
                    break;
                case RuleKind.Delete:
                    _draft.BySeparator = BySepBox.IsChecked == true;
                    _draft.Separator = SepBox.Text;
                    _draft.Segment = Math.Max(1, I(SegBox.Text, 1));
                    _draft.From = Math.Max(1, I(DelFrom.Text, 1));
                    _draft.Count = Math.Max(0, I(DelCount.Text, 0));
                    break;
                case RuleKind.Number:
                    _draft.Start = I(NumStart.Text, 1);
                    _draft.Step = I(NumStep.Text, 1);
                    _draft.Digits = Math.Max(1, I(NumDigits.Text, 2));
                    _draft.NumAt = (InsertAt)Math.Max(0, NumAtBox.SelectedIndex);
                    _draft.Pos = I(NumPos.Text, 0);
                    _draft.ReplaceName = _draft.NumAt == InsertAt.ReplaceAll;
                    break;
                case RuleKind.Date:
                    _draft.DateFrom = (DateSource)Math.Max(0, DateFromBox.SelectedIndex);
                    _draft.DateFormat = DateFmt.Text;
                    _draft.DateAt = DateAtBox.SelectedIndex == 1 ? InsertAt.End : InsertAt.Start;
                    break;
                case RuleKind.Case:
                    _draft.Case = (CaseMode)Math.Max(0, CaseBox.SelectedIndex);
                    break;
                case RuleKind.Clean:
                    _draft.TrimSpace = TrimBox.IsChecked == true;
                    _draft.CollapseSpace = CollapseBox.IsChecked == true;
                    _draft.RemoveBrackets = BracketBox.IsChecked == true;
                    _draft.RemoveSpecial = SpecialBox.IsChecked == true;
                    _draft.FullWidth = WidthBox.IsChecked == true;
                    break;
                case RuleKind.Ext:
                    _draft.NewExt = ExtBox.Text;
                    break;
            }

            _target.CopyFrom(_draft);
            DialogResult = true;
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
