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
    // Two navigation legs, one field task, and the return flight target a three-to-five-minute first playthrough.
    public static readonly StoryDefinition[] All =
    {
        S("01", "WHITEOUT", "暖房を失った山岳救護所に\n親子と救助隊が取り残された。", "雪崩監視所へ。母親：「娘が眠り始めました」", "無線尾根へ。救護所の室温が氷点下へ落ちる", "血液と保温剤を救護所へ投下", "投下箱を抱いた少女が窓越しに手を振った。", StoryAction.Deliver, 0, 2, 1, 0.9f, 1f),
        S("02", "SILENT RIDGE", "吹雪で中継局が沈黙。\n捜索隊12名の通信が途絶えた。", "森林シェルターへ。12名の呼出符号を照合", "山岳ゲートを越える。無線には風音しかない", "無線尾根で通信を14秒中継", "雑音の中から12人の「了解」が順に戻った。", StoryAction.Relay, 7, 0, 2, 0.75f, 0.9f, 14f),
        S("03", "BLUE ICE", "氷湖に亀裂が発生。\n避難車列の安全な道を探す。", "監視所へ。車列には子どもを乗せたバスがいる", "雪崩棚の風下へ。氷の割れる音が近づく", "氷湖を12秒静止スキャン", "最後のバスが渡った3分後、湖面は崩れた。", StoryAction.Scan, 0, 6, 3, 0.55f, 0.65f, 12f),
        S("04", "LAST INSULIN", "診療所のインスリンは残り1本。\n患者の容体が悪化している。", "森林ルートへ。保冷箱の温度はまだ安全域", "気象台へ。医師：「次の発作には間に合わない」", "インスリン保冷箱を救護所へ投下", "少年は震える手で母親の指を握り返した。", StoryAction.Deliver, 7, 5, 1, 0.6f, 0.7f),
        S("05", "AVALANCHE LINE", "ヘリ救助地点へ雪崩が迫る。\n新しい着陸線を示せ。", "気象台へ。斜面警報が赤へ変わる", "山岳ゲートへ。ヘリは燃料限界まで6分", "雪崩棚に発光マーカーを設置", "雪煙の直前、ヘリのスキッドが尾根に触れた。", StoryAction.Mark, 5, 0, 6, 0.95f, 0.85f),
        S("06", "DARK MINE", "旧鉱山で保守員3名が閉じ込められた。\n坑内酸素はあと20分。", "森林シェルターへ。坑内から壁を叩く音を受信", "監視所の低空航路へ。応答音が一つ消える", "酸素ボンベを坑口へ投下", "暗い坑口の奥で、三つのライトが再び瞬いた。", StoryAction.Deliver, 7, 0, 4, 0.7f, 0.75f),
        S("07", "LOST SIGNAL", "救難ビーコンが雪に埋もれた。\n信号は数分おきにしか届かない。", "山岳ゲートへ。弱い信号が一度だけ応答", "雪崩棚へ。反射波の奥に人の呼吸音が混じる", "気象台周辺を10秒スキャン", "雪洞を掘り当てた隊員から、泣き声が届いた。", StoryAction.Scan, 0, 6, 5, 0.85f, 0.95f, 10f),
        S("08", "WINTER HEART", "救助隊長が胸痛を訴えている。\n尾根越えの搬送には耐えられない。", "森林ルートへ。隊長は部下の撤退を先に命じる", "無線尾根へ。医師が途切れる脈を読み上げる", "心臓薬を救護所へ投下", "隊長の最初の言葉は「全員戻ったか」だった。", StoryAction.Deliver, 7, 2, 1, 0.5f, 0.6f),
        S("09", "BROKEN HEATER", "森林避難所の暖房が停止。\n夜温は氷点下25度まで下がる。", "監視所へ。避難所では互いの体温を分けている", "気象台へ。谷風で到達予報が2分遅れる", "燃料電池を森林避難所へ投下", "煙突の煙とともに、窓へ橙色の灯りが戻った。", StoryAction.Deliver, 0, 5, 7, 0.65f, 0.8f),
        S("10", "NIGHT WATCH", "夜間捜索隊が谷で分断された。\n携帯無線は尾根を越えない。", "森林シェルターへ。片方の隊が仲間の名を呼ぶ", "無線尾根へ。もう片方から短い返答を受信", "気象台回線を16秒中継", "二つの隊の声が重なり、同じ帰還路を選んだ。", StoryAction.Relay, 7, 2, 5, 0.8f, 0.7f, 16f),
        S("11", "RED FLARE", "氷湖の赤い信号は単独登山者。\n低体温で歩けない。", "監視所へ。遭難者：「眠らないよう話してくれ」", "雪崩棚を風上から横断。返事が次第に短くなる", "保温毛布と発熱剤を氷湖へ投下", "毛布に包まれた登山者は、ヘリの灯りを数え続けた。", StoryAction.Deliver, 0, 6, 3, 1f, 0.9f),
        S("12", "ICE DAM", "氷の堰が谷川を塞いだ。\n決壊すれば麓の集落が危ない。", "気象台へ。学校の避難完了まであと13分", "雪崩棚へ。亀裂が川幅いっぱいに伸びる", "氷湖を15秒測量スキャン", "警報は決壊の11分前に届き、校庭は空になった。", StoryAction.Scan, 5, 6, 3, 0.6f, 0.55f, 15f),
        S("13", "MEDIVAC PATH", "負傷者搬送ヘリが雲底下で迷っている。\n着陸進入路を作れ。", "無線尾根へ。操縦士：「計器以外は真っ白だ」", "気象台へ。負傷者の意識レベルが低下", "雪崩棚に進入マーカーを設置", "ヘリは光の列をたどり、一度の進入で収容した。", StoryAction.Mark, 2, 5, 6, 0.9f, 1f),
        S("14", "QUIET CABIN", "森林小屋の管理人から定時連絡がない。\n心臓の持病がある。", "監視所へ。入山記録には管理人一人だけ", "無線尾根へ。七度目の呼出しに微かな咳が返る", "処方薬を森林小屋へ投下", "管理人は薬を掲げ、無線越しに「春に会おう」と笑った。", StoryAction.Deliver, 0, 2, 7, 0.45f, 0.5f),
        S("15", "SECOND CHANCE", "先行ドローンが雪崩棚で墜落。\n記録には遭難者の位置が残る。", "森林ルートへ。墜落機の操縦者が航跡を読み上げる", "旧鉱山へ。破損した機体信号がもう一度灯る", "雪崩棚を12秒精密スキャン", "失われた機体の記録が、三人の捜索範囲を絞った。", StoryAction.Scan, 7, 4, 6, 0.85f, 0.9f, 12f),
        S("16", "AFTERSHOCK", "余震で旧鉱山の救出路が崩れた。\n二次崩落の危険を調べる。", "森林シェルターへ。坑内の家族へ救助員が声を届ける", "監視所へ。斜面変位が警戒線を越える", "坑口を10秒構造スキャン", "安全な東坑道が見つかり、壁の向こうから拍手が響いた。", StoryAction.Scan, 7, 0, 4, 0.7f, 0.65f, 10f),
        S("17", "FROZEN ANTENNA", "着氷で山頂アンテナが停止。\n気象警報が麓へ届かない。", "監視所へ。麓では警報を知らず除雪が続く", "気象台へ。着氷が予備回線にも広がる", "除氷ユニットを無線尾根へ投下", "谷のサイレンが鳴り、除雪車が一斉に引き返した。", StoryAction.Deliver, 0, 5, 2, 0.95f, 0.85f),
        S("18", "MORNING STAR", "早産児の呼吸補助薬が必要。\n道路復旧は夜明けに間に合わない。", "森林ルートへ。父親が無線で子守歌を続ける", "監視所へ。救護所の小さな窓灯りを確認", "新生児用薬剤を救護所へ投下", "薬が届き、無線には子守歌と小さな呼吸だけが残った。", StoryAction.Deliver, 7, 0, 1, 0.55f, 0.75f),
        S("19", "WHITE ECHO", "校外学習の6名が下山していない。\n雪面の反響で声の方向が読めない。", "無線尾根へ。最終通話の背後に校歌が聞こえる", "雪崩棚へ。六人分の足跡が林へ続く", "森林シェルターを14秒熱源スキャン", "六つの熱源。子どもたちは肩を寄せ、まだ歌っていた。", StoryAction.Scan, 2, 6, 7, 0.8f, 0.95f, 14f),
        S("20", "LAST WINDOW", "嵐の切れ間はあと5分。\n救護所には解毒剤を待つ患者がいる。", "気象台へ。雲の切れ間は予報より38秒短い", "監視所を最短通過。患者の呼吸が浅くなる", "解毒剤を救護所へ投下", "雲が閉じる直前に箱が届き、その場で治療が始まった。", StoryAction.Deliver, 5, 0, 1, 1.05f, 1f),
        S("21", "EMBER UNDER SNOW", "旧鉱山の発電機が火災を起こした。\n救護所の電力も同じ系統だ。", "森林シェルターへ。消火剤を積み、風上を確認", "旧鉱山へ。炎が雪の下の燃料管を走る", "消火カプセルを救護所系統へ投下", "炎は燃料庫の手前で消え、救護所の灯りは残った。", StoryAction.Deliver, 7, 4, 1, 0.75f, 0.8f),
        S("22", "THE GUIDE", "視界ゼロのヘリを尾根越しに誘導する。\nこちらの映像だけが目になる。", "無線尾根へ。操縦士：「君の映像を信じる」", "気象台へ。雲底とローターの間はわずか30m", "雪崩棚で18秒映像中継", "ヘリは映像の地平線をなぞり、雲の外へ抜けた。", StoryAction.Relay, 2, 5, 6, 1f, 1f, 18f),
        S("23", "THIN AIR", "高所作業員が急性低酸素症を発症。\n自力で尾根を下れない。", "監視所へ。仲間が残り酸素を一呼吸ずつ分ける", "雪崩棚へ。下降気流で投下箱が大きく揺れる", "携帯酸素を無線尾根へ投下", "酸素飽和度が戻ると、最初に仲間の名前を呼んだ。", StoryAction.Deliver, 0, 6, 2, 0.9f, 0.85f),
        S("24", "FIRST LIGHT", "嵐が去る。次の雪崩に備え\n夜明けの観測網を完成させる。", "旧鉱山へ。救った作業員が地震計を再起動する", "森林シェルターへ。子どもたちが積雪計を見守る", "気象台に最終観測点を設置", "朝日と同時に、救った人々の声が山じゅうから届いた。", StoryAction.Mark, 4, 7, 5, 0.4f, 0.25f)
    };

    private static StoryDefinition S(string code, string title, string briefing, string routeOne, string routeTwo,
        string objective, string success, StoryAction action, int firstSite, int secondSite, int targetSite,
        float wind, float storm, float holdSeconds = 0f)
        => new(code, title, briefing, routeOne, routeTwo, objective, success, action, firstSite, secondSite, targetSite, wind, storm, holdSeconds);
}
