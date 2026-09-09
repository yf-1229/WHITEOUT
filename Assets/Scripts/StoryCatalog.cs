public enum StoryAction { Deliver, Scan, Relay, Mark }

public readonly struct StoryDefinition
{
    public readonly string code;
    public readonly string title;
    public readonly string briefing;
    public readonly string routeOne;
    public readonly string routeTwo;
    public readonly string objective;
    public readonly string success;
    public readonly StoryAction action;
    public readonly int firstSite;
    public readonly int secondSite;
    public readonly int targetSite;
    public readonly float wind;
    public readonly float storm;
    public readonly float holdSeconds;

    public StoryDefinition(string code, string title, string briefing, string routeOne, string routeTwo,
        string objective, string success, StoryAction action, int firstSite, int secondSite, int targetSite,
        float wind, float storm, float holdSeconds = 0f)
    {
        this.code = code;
        this.title = title;
        this.briefing = briefing;
        this.routeOne = routeOne;
        this.routeTwo = routeTwo;
        this.objective = objective;
        this.success = success;
        this.action = action;
        this.firstSite = firstSite;
        this.secondSite = secondSite;
        this.targetSite = targetSite;
        this.wind = wind;
        this.storm = storm;
        this.holdSeconds = holdSeconds;
    }
}

public static class StoryCatalog
{
    // Two navigation legs, one field task, and the return flight target a five-minute first playthrough.
    public static readonly StoryDefinition[] All =
    {
        S("01", "WHITEOUT", "暖房を失った山岳救護所に\n親子と救助隊が取り残された。", "雪崩監視所の航路を確認", "無線尾根で最後の気象報告を受信", "血液と保温剤を救護所へ投下", "全員が夜明け前に搬送された。", StoryAction.Deliver, 0, 2, 1, 0.9f, 1f),
        S("02", "SILENT RIDGE", "吹雪で中継局が沈黙。\n捜索隊12名の通信が途絶えた。", "森林シェルター上空へ", "山岳ゲートを越えて中継局へ", "無線尾根で通信を14秒中継", "12名の位置情報が一斉に戻った。", StoryAction.Relay, 7, 0, 2, 0.75f, 0.9f, 14f),
        S("03", "BLUE ICE", "氷湖に亀裂が発生。\n避難車列の安全な道を探す。", "監視所で氷温データを取得", "雪崩棚の風下へ回り込む", "氷湖を12秒静止スキャン", "車列は崩落の3分前に湖を離れた。", StoryAction.Scan, 0, 6, 3, 0.55f, 0.65f, 12f),
        S("04", "LAST INSULIN", "診療所のインスリンは残り1本。\n患者の容体が悪化している。", "森林ルートで着氷を避ける", "気象台から風向更新を受信", "インスリン保冷箱を救護所へ投下", "薬は適温のまま医師の手に届いた。", StoryAction.Deliver, 7, 5, 1, 0.6f, 0.7f),
        S("05", "AVALANCHE LINE", "ヘリ救助地点へ雪崩が迫る。\n新しい着陸線を示せ。", "気象台で斜面警報を確認", "山岳ゲートから尾根へ上昇", "雪崩棚に発光マーカーを設置", "救助ヘリは安全な尾根へ着陸した。", StoryAction.Mark, 5, 0, 6, 0.95f, 0.85f),
        S("06", "DARK MINE", "旧鉱山で保守員3名が閉じ込められた。\n坑内酸素はあと20分。", "森林シェルターで予備灯を受領", "監視所の低空航路を通過", "酸素ボンベを坑口へ投下", "坑内から三つのライトが応答した。", StoryAction.Deliver, 7, 0, 4, 0.7f, 0.75f),
        S("07", "LOST SIGNAL", "救難ビーコンが雪に埋もれた。\n信号は数分おきにしか届かない。", "山岳ゲートで信号方位を固定", "雪崩棚の反射波を除外", "気象台周辺を10秒スキャン", "雪洞の位置が救助隊へ送信された。", StoryAction.Scan, 0, 6, 5, 0.85f, 0.95f, 10f),
        S("08", "WINTER HEART", "救助隊長が胸痛を訴えている。\n尾根越えの搬送には耐えられない。", "森林ルートで乱気流を回避", "無線尾根で医師と接続", "心臓薬を救護所へ投下", "投薬後、隊長の心拍は安定した。", StoryAction.Deliver, 7, 2, 1, 0.5f, 0.6f),
        S("09", "BROKEN HEATER", "森林避難所の暖房が停止。\n夜温は氷点下25度まで下がる。", "監視所で積雪高度を確認", "気象台から谷風データを取得", "燃料電池を森林避難所へ投下", "避難所の煙突に灯りが戻った。", StoryAction.Deliver, 0, 5, 7, 0.65f, 0.8f),
        S("10", "NIGHT WATCH", "夜間捜索隊が谷で分断された。\n携帯無線は尾根を越えない。", "森林シェルターの隊を捕捉", "無線尾根へ上昇", "気象台回線を16秒中継", "二つの隊は同じ帰還路を共有できた。", StoryAction.Relay, 7, 2, 5, 0.8f, 0.7f, 16f),
        S("11", "RED FLARE", "氷湖の赤い信号は単独登山者。\n低体温で歩けない。", "監視所で救難信号を照合", "雪崩棚を風上から横断", "保温毛布と発熱剤を氷湖へ投下", "登山者はヘリ到着まで意識を保った。", StoryAction.Deliver, 0, 6, 3, 1f, 0.9f),
        S("12", "ICE DAM", "氷の堰が谷川を塞いだ。\n決壊すれば麓の集落が危ない。", "気象台で水位推定を受信", "雪崩棚から亀裂を確認", "氷湖を15秒測量スキャン", "避難警報は決壊より11分早く届いた。", StoryAction.Scan, 5, 6, 3, 0.6f, 0.55f, 15f),
        S("13", "MEDIVAC PATH", "負傷者搬送ヘリが雲底下で迷っている。\n着陸進入路を作れ。", "無線尾根でヘリを捕捉", "気象台で視程を確認", "雪崩棚に進入マーカーを設置", "ヘリは一度の進入で負傷者を収容した。", StoryAction.Mark, 2, 5, 6, 0.9f, 1f),
        S("14", "QUIET CABIN", "森林小屋の管理人から定時連絡がない。\n心臓の持病がある。", "監視所の記録で入山を確認", "無線尾根から小屋を呼び出す", "処方薬を森林小屋へ投下", "管理人は無線越しに短く礼を告げた。", StoryAction.Deliver, 0, 2, 7, 0.45f, 0.5f),
        S("15", "SECOND CHANCE", "先行ドローンが雪崩棚で墜落。\n記録には遭難者の位置が残る。", "森林ルートから捜索開始", "旧鉱山で機体信号を再捕捉", "雪崩棚を12秒精密スキャン", "記録を回収し捜索範囲を半分に絞った。", StoryAction.Scan, 7, 4, 6, 0.85f, 0.9f, 12f),
        S("16", "AFTERSHOCK", "余震で旧鉱山の救出路が崩れた。\n二次崩落の危険を調べる。", "森林シェルターで地震計と同期", "監視所で斜面変位を確認", "坑口を10秒構造スキャン", "安全な東坑道から救出が再開された。", StoryAction.Scan, 7, 0, 4, 0.7f, 0.65f, 10f),
        S("17", "FROZEN ANTENNA", "着氷で山頂アンテナが停止。\n気象警報が麓へ届かない。", "監視所で交換部品を検査", "気象台の保守回線へ接続", "除氷ユニットを無線尾根へ投下", "警報サイレンが谷全体に響いた。", StoryAction.Deliver, 0, 5, 2, 0.95f, 0.85f),
        S("18", "MORNING STAR", "早産児の呼吸補助薬が必要。\n道路復旧は夜明けに間に合わない。", "森林ルートで振動を抑える", "監視所で救護所の灯りを確認", "新生児用薬剤を救護所へ投下", "小さな呼吸は朝まで途切れなかった。", StoryAction.Deliver, 7, 0, 1, 0.55f, 0.75f),
        S("19", "WHITE ECHO", "校外学習の6名が下山していない。\n雪面の反響で声の方向が読めない。", "無線尾根で最終通話を再生", "雪崩棚で足跡を捕捉", "森林シェルターを14秒熱源スキャン", "六つの熱源を確認、全員無事だった。", StoryAction.Scan, 2, 6, 7, 0.8f, 0.95f, 14f),
        S("20", "LAST WINDOW", "嵐の切れ間はあと5分。\n救護所には解毒剤を待つ患者がいる。", "気象台で短い晴れ間を確認", "監視所の直上を最短通過", "解毒剤を救護所へ投下", "窓が閉じる直前、治療が始まった。", StoryAction.Deliver, 5, 0, 1, 1.05f, 1f),
        S("21", "EMBER UNDER SNOW", "旧鉱山の発電機が火災を起こした。\n救護所の電力も同じ系統だ。", "森林シェルターで消火剤を受領", "旧鉱山上空で煙の流れを読む", "消火カプセルを救護所系統へ投下", "炎は燃料庫へ届く前に消えた。", StoryAction.Deliver, 7, 4, 1, 0.75f, 0.8f),
        S("22", "THE GUIDE", "視界ゼロのヘリを尾根越しに誘導する。\nこちらの映像だけが目になる。", "無線尾根で操縦士と接続", "気象台から雲底を取得", "雪崩棚で18秒映像中継", "ヘリは雲を抜け、救護所を発見した。", StoryAction.Relay, 2, 5, 6, 1f, 1f, 18f),
        S("23", "THIN AIR", "高所作業員が急性低酸素症を発症。\n自力で尾根を下れない。", "監視所で酸素残量を確認", "雪崩棚の下降気流を横断", "携帯酸素を無線尾根へ投下", "酸素飽和度が戻り搬送が始まった。", StoryAction.Deliver, 0, 6, 2, 0.9f, 0.85f),
        S("24", "FIRST LIGHT", "嵐が去る。次の雪崩に備え\n夜明けの観測網を完成させる。", "旧鉱山の地震計を確認", "森林シェルターの積雪計を確認", "気象台に最終観測点を設置", "朝日と同時に山全体がオンラインになった。", StoryAction.Mark, 4, 7, 5, 0.4f, 0.25f)
    };

    private static StoryDefinition S(string code, string title, string briefing, string routeOne, string routeTwo,
        string objective, string success, StoryAction action, int firstSite, int secondSite, int targetSite,
        float wind, float storm, float holdSeconds = 0f)
        => new(code, title, briefing, routeOne, routeTwo, objective, success, action, firstSite, secondSite, targetSite, wind, storm, holdSeconds);
}
