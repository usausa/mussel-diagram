namespace MusselDiagram.Languages.Japanese;

public static class JapaneseLanguage
{
    public static DiagramTexts Texts { get; } = new()
    {
        Reasons = new Dictionary<DiagramReasonKind, string>
        {
            [DiagramReasonKind.NoConnectionPoint] = "ここにはつなぐ所がありません",
            [DiagramReasonKind.Direction] = "端の向きが合いません",
            [DiagramReasonKind.PortKind] = "ポートの種類が違います",
            [DiagramReasonKind.Permission] = "この要素は変えられません",
            [DiagramReasonKind.Limit] = "このポートにはこれ以上つなげません",
            [DiagramReasonKind.SelfOrDuplicate] = "自分どうしか、すでにある線と同じつなぎ方です",
            [DiagramReasonKind.PeerType] = "このポートはこの型を受け付けません",
            [DiagramReasonKind.CrossParent] = "両端が別の群にあります",
            [DiagramReasonKind.Cycle] = "つなぐと循環になります",
            [DiagramReasonKind.Count] = "この型は上限の数まで置かれています",
            [DiagramReasonKind.Nesting] = "ここには置けません",
            [DiagramReasonKind.Changed] = "図が変わりました。もう一度行ってください",
            [DiagramReasonKind.Canceled] = "変更できませんでした",
            [DiagramReasonKind.TooLarge] = "画像が大きすぎて、ブラウザーで描けません"
        },
        Issues = new Dictionary<DiagramIssueKind, string>
        {
            [DiagramIssueKind.ConnectionRule] = "線が接続の規則に合いません({0})",
            [DiagramIssueKind.NestingRule] = "要素が入れ子の規則に合いません({0})",
            [DiagramIssueKind.MaxCount] = "型「{0}」の要素が {1} 個を超えています",
            [DiagramIssueKind.RequiredType] = "型「{0}」の要素が要ります",
            [DiagramIssueKind.RequiredPort] = "ポート「{0}」に線をつないでください",
            [DiagramIssueKind.MissingPort] = "ポート「{0}」がありません",
            [DiagramIssueKind.MissingSharedItem] = "共有の要素「{0}」がありません",
            [DiagramIssueKind.UnregisteredType] = "型「{0}」は登録されていません",
            [DiagramIssueKind.DuplicateId] = "ID「{0}」が重なっています",
            [DiagramIssueKind.MissingEnd] = "線の端の要素がありません",
            [DiagramIssueKind.InvalidParent] = "親「{0}」がないか、木の群ではありません",
            [DiagramIssueKind.InvalidMember] = "中身「{0}」がないか、ノードでも木の群でもありません",
            [DiagramIssueKind.NoMembers] = "重ね合わせの群に中身がありません",
            [DiagramIssueKind.KindMismatch] = "群の種類が、型「{0}」の種類と違います",
            [DiagramIssueKind.InvalidSize] = "大きさが正の数ではありません",
            [DiagramIssueKind.UnreadableData] = "データを型「{0}」のデータとして読めません",
            [DiagramIssueKind.RemovedSvgContent] = "アイコン「{0}」から消したもの: {1}",
            [DiagramIssueKind.RejectedUrl] = "アイコン「{0}」の使わなかった URL: {1}",
            [DiagramIssueKind.UnreadableIcon] = "アイコン「{0}」は SVG として読めません",
            [DiagramIssueKind.ImageNotEmbedded] = "画像「{0}」を入れられませんでした"
        },
        Commands = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Undo"] = "元に戻す",
            ["Redo"] = "やり直す",
            ["Delete"] = "削除",
            ["Cut"] = "切り取り",
            ["Copy"] = "コピー",
            ["Paste"] = "貼り付け",
            ["SelectAll"] = "すべて選ぶ",
            ["ClearSelection"] = "選択を外す",
            ["EditText"] = "文字を編集",
            ["QuickAdd"] = "探して置く",
            ["Group"] = "グループ化",
            ["Ungroup"] = "グループ解除",
            ["CreateOverlayGroup"] = "重ね合わせの群を作る",
            ["AddMembers"] = "群に足す",
            ["RemoveMembers"] = "群から外す",
            ["BringToFront"] = "最前面へ",
            ["SendToBack"] = "最背面へ",
            ["BringForward"] = "1 つ前へ",
            ["SendBackward"] = "1 つ後ろへ",
            ["AlignLeft"] = "左に揃える",
            ["AlignCenter"] = "左右の中央に揃える",
            ["AlignRight"] = "右に揃える",
            ["AlignTop"] = "上に揃える",
            ["AlignMiddle"] = "上下の中央に揃える",
            ["AlignBottom"] = "下に揃える",
            ["DistributeHorizontally"] = "左右に等間隔",
            ["DistributeVertically"] = "上下に等間隔",
            ["ToggleLock"] = "ロックの切り替え",
            ["ZoomIn"] = "拡大",
            ["ZoomOut"] = "縮小",
            ["ResetZoom"] = "100% で表示",
            ["ZoomToFit"] = "全体を表示",
            ["ZoomToSelection"] = "選んだ要素を表示",
            ["NextIssue"] = "次の問題",
            ["PreviousIssue"] = "前の問題"
        },
        SearchPlaceholder = "検索",
        NoMatches = "見つかりません"
    };
}
