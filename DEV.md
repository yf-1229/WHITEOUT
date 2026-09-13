# 開発者向け手順

対象プロジェクトは Unity 6 + OpenXR の Quest 2 / Quest 3S 向け雪山救助プロトタイプです。Unity の最低バージョンとOpenXR推奨構成は [Meta公式Unity要件](https://developers.meta.com/horizon/documentation/unity/unity-development-requirements/) に合わせます。

## 1. 開発環境

### 必要なもの

- Unity Hub
- Unity `6000.0.66f2` 以降
- Android Build Support、Android SDK & NDK Tools、OpenJDK
- Meta Quest 2またはMeta Quest 3S
- USB-Cデータケーブル
- Meta Quest Developer Hub（MQDH）またはADB
- Meta開発者アカウントとDeveloper Modeを有効にしたQuest

### プロジェクト設定

1. Unityで `/home/yuta/Documents/drone` を開く。
2. Package Managerの依存パッケージ解決が完了するまで待つ。
3. `Edit > Project Settings > XR Plug-in Management` を開き、AndroidにOpenXR、Meta Quest Support、`Oculus Touch Controller Profile`、`Meta Quest Touch Plus Controller Profile`が有効なことを確認する。これらとIL2CPP、ARM64、API 29、横向きは設定済み。
4. `Edit > Project Settings > Player > Other Settings` でPackage Name（例 `com.yourstudio.dronejobs`）を設定する。

Metaの公開向けUnity設定でもIL2CPPとARM64が要求されます。[Prepare Your App for Publishing](https://developers.meta.com/horizon/documentation/unity/unity-prepare-for-publish/)

## 2. 実機ビルド

### Questの準備

1. QuestモバイルアプリでDeveloper Modeを有効にする。
2. ヘッドセットを起動し、USBデバッグ許可を承認する。
3. MQDHでQuestが接続済みになっていることを確認する。

### UnityからBuild & Run

1. `File > Build Profiles` を開く。
2. Androidプロファイルを選択し、`Assets/Scenes/Prototype.unity` がScenes In Buildに含まれていることを確認する。
3. `Run Device` に接続中のQuest 2またはQuest 3Sを選択する。
4. `Build And Run` を実行する。
5. Questのアプリ一覧から起動し、下記の実機確認を行う。

### APKを手動インストールする場合

```bash
adb devices
adb install -r Builds/DroneJobsVR.apk
adb shell monkey -p com.yourstudio.dronejobs 1
```

`adb devices` が `unauthorized` の場合は、ヘッドセット内のUSBデバッグ許可を再承認する。複数端末がある場合は `adb -s <serial>` を付ける。

## 3. VR表示確認チェックリスト

### 起動・トラッキング

- 起動直後にカメラが床へ埋まらない
- 操縦者カメラは基地標高ではなく設置地点のTerrainから1.55m上に置かれる
- `QuestPoseDriver` により頭部の位置・回転が反映される
- 座位と立位の両方で視線が自然な高さになる
- Guardian境界や実空間を確認できる
- 画面の文字とFPVモニターが視野内に収まる

### 操作

- 起動直後は`START FLIGHT`、`PRACTICE RANGE`、`SETTINGS`を選べるTOPメニューが表示される
- A：メニュー選択／結果画面から再出動
- X：メニューで次項目／任務中は2秒以内に2回押して中止／成功後に次話へ
- 右スティック押し込み：メニューで前項目
- 飛行モードは`SETTINGS`またはミッション選択の`FLIGHT`で切り替える
- 右グリップ：操縦者、フルFPV、三人称追従を切り替える
- 左スティック：アシスト時は高度・ヨー、マニュアル時はスロットル・ヨー
- 右スティック：アシスト時は進行方向、マニュアル時は目標ピッチ・ロール角速度（姿勢を戻さず、機体を傾けて推力方向を変える）
- アシスト時は前進・後進中もRigidbody姿勢から傾斜分の推力を補正し、同じ高度を維持する
- 右トリガー：物資投下／スキャン確定／標識設置
- B：自動帰還・着陸、着陸後にもう一度押すと離陸
- 自動帰還中はRTH表示が出て、進行方向24m以内のTerrainから地上12m以上の安全高度を選ぶ
- 森林では幹・枝葉をすり抜けず、接触速度に応じて振動・衝突・墜落になる
- キーボード検証時はREADMEのキー割り当てを使う

### ミッション

- 24話を前後に選択し、各話を開始・再出動できる
- 任務中にXを2回押すと、誤操作を防ぎながらフリーフライトへ戻れる
- 全話が二つの航法地点、現地作業、基地帰還の順で進む
- 割り込み救難の発信中だけ、対象地点の地表に遭難者と緊急毛布が現れる
- 物資投下、静止スキャン、通信中継、標識設置がそれぞれ完了できる
- 静止スキャンはサーマルON時だけ進み、スキャン・中継・救難ホバリング中だけ中央の細ゲージが表示される
- サーマルONでFPV映像全体が薄い寒色へ変わり、対象地点の熱源だけが照明非依存の橙色で見える
- 物資投下・標識設置では目標10m以内だけ中央にSTABILIZE／READYが表示され、実行条件と一致する
- 帰還：発着地点の連続した反射塗装リングへ自動着陸してプロペラが止まり、Bで再離陸できる
- 誤投下が即クラッシュにならず減点される
- 強い衝突、電池切れで失敗画面になる
- リトライで位置、荷物、電池、スコアが初期化される
- リトライ・再出動では物理補間履歴も破棄され、機体とFPV映像が水平姿勢から始まる
- 救援成功後に物語の結末、スコア、グレードが表示される
- 各話のベストスコアが個別にローカル保存される
- 各話のベストスコアが救助ネットワークの完了数・準備率へ反映される
- 救護所・森林小屋を対象にした任務の初回成功後、再起動しても現地の窓が明るく灯る
- メニューで各話のクリア状況とベストスコアを確認できる
- HUDのNAV矢印だけで吹雪中も目標へ旋回できる
- 着陸後はHUDにモーター停止と再始動操作が表示される
- ミッション説明は作戦番号・任務名・目的・進捗・操作が視線順に読め、脅威欄や長文テレメトリを置かない
- チェックポイントごとの無線字幕が数秒で消え、成功の結末は基地着陸後まで表示されない
- フルFPV・追従視点では大型作戦カードを隠し、FPV計器と無線字幕だけを表示する
- 着陸パッドは非発光の反射塗装で、目標地点の単一スポットライトだけが弱く点灯する
- 高度付き航法点でもパッド模型は空中へ浮かず、地表に密着する
- 各パッドと反射塗装が配置地点のTerrain法線へ沿い、斜面で雪面を突き抜けない

### 快適性・性能

- 操縦者視点では10分連続プレイしてもカメラが不必要に移動しない
- フルFPVは黒枠なしの全面映像、三人称追従だけ周辺の快適枠が表示され、右グリップで即座に操縦者視点へ戻せる
- フルFPVと操縦者モニターで、機首カメラが機体のピッチ・ロール・ヨーへ遅延なく追従する
- 上昇操作とバンク時の傾斜補正で、ローター回転数・モーター音が移動速度より先に上がる
- `Window > Analysis > Profiler` のAndroid接続で72fpsを概ね維持する
- Terrainの草、針葉樹、吹雪が左右の目で破綻せず、強い吹雪でも72fpsを概ね維持する
- 操縦者視点ではFPV RenderTextureが30fpsで更新され、他視点では不要な描画が停止する
- Quest 2 / Quest 3Sで発熱、メモリ増加、音切れがない

UnityエディタのGameビューはQuest実機の代替にならない。最終確認は必ず対象のQuest 2 / Quest 3Sで行う。

### 画質を維持する描画最適化

- 背後・視野外の除外はUnity標準のカメラごとのフラスタムカリングに任せる。主視点から見えない物体でもFPV映像や画面内の影に必要な場合があるため、物体自体を無効化しない。
- 同じ材質の木・機体部品はマテリアルを共有し、岩はノイズテクスチャも共有する。同じ色の着陸リングもマテリアルを再利用する。
- `StaticScenery`配下の木・岩・建物・着陸リングを生成時に静的バッチ化する。物体ごとのカリング境界を維持し、機体・荷物・サーマル対象は含めない。静的バッチは頂点データを複製するため、景観を大幅に増やす場合はメモリを計測してインスタンシングを検討する。
- FPVカメラは単眼・512x288・2x MSAA・30fpsで描画し、操縦者視点ではHUDを映像内へ合成する。再帰描画を避けるため、FPVモニター自体は描画時だけ隠す。
- XR解像度、主カメラの表示距離、4x MSAA、影、地形精度、草・吹雪の量は変更していない。

Unityの[視野外カリング](https://docs.unity3d.com/6000.0/Documentation/Manual/OcclusionCulling.html)と[静的バッチAPI](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/StaticBatchingUtility.html)を使用している。実行時生成シーンに対するベイク済み遮蔽カリングは導入していない。

`python3 tools/validate_prototype.py`に加え、Unityを`-batchmode -projectPath <project> -executeMethod ProjectConfigurator.SmokePlayMode -logFile <log>`で起動すると、Play Modeで資源の共有、静的バッチ、個別の境界、動く部品の除外、FPVのレイヤーと画質設定を検証する。画面の黒転を検査するためGPU描画が必要で、`-nographics`は付けない。`RENDER CHECK`にマテリアル・テクスチャ・バッチ対象の数を出力する。GPUの描画時間はこの検証では測れないため、QuestのProfiler/Frame Debuggerで操縦者・FPV・追従、180度旋回、吹雪、サーマル切替を比較する。

Unity 6000.6.0f1のPlay Modeで、MeshRenderer 161個、共有マテリアル36個、共有テクスチャ15個、静的バッチ対象120個を確認した。この件数にはTerrain・吹雪・Canvas用の資源は含まれず、FPSや総メモリの改善率を示すものではない。

検証時の制限：描画の全アサーション、Pythonチェック、Unity 6000.6.0f1のPlay Modeスモークは終了コード0で通過した。Linux64統合ビルドもSuccessとなり、生成プレイヤーをバッチモードで15秒起動してゲームコード例外がないことを確認した。Questでの見た目・FPS・総メモリ比較は未実施。

## 4. リリース用ビルド

1. `Development Build` と `Script Debugging` を無効化する。
2. Android Package Nameを一度決めたら変更しない。
3. Keystoreを作成し、パスワードとファイルを安全にバックアップする。
4. Version、Bundle Version Codeを更新する。更新ごとにVersion Codeを増やす。
5. Release APKまたはAABを生成する。
6. MQDHまたはMetaのアップロード手段でALPHAへアップロードする。
7. Quest実機でクリーンインストール、更新インストール、アンインストール後の再起動を確認する。

Keystoreを紛失すると同じアプリを更新できなくなるため、リポジトリへコミットしない。MetaもQuest公開には署名用Keystoreが必須と説明しています。[Required app configuration](https://developers.meta.com/horizon/resources/publish-overview-appID/)

## 5. Meta Horizon Store申請

### 申請前の準備

- Meta Horizon Developer Dashboardで組織を作成・認証する
- `Create a New App` でMeta Horizon Store向けアプリを作成する
- App IDとPackage NameをUnity側の設定と一致させる
- 開発者連絡先、Webサイト、プライバシーポリシーURLを用意する
- アプリアイコン、カバー画像、スクリーンショット、紹介動画を用意する
- 日本語の短文説明、長文説明、検索キーワードを用意する
- Category、Genre、Game Modes、Supported Controllers、Play Area、Comfort Levelを記入する
- IARCコンテンツレーティングを取得する
- 使用する権限とデータ収集を確認し、Data Use Checkupを完了する

メタデータの入力項目と文字数制限は [Providing App Metadata](https://developers.meta.com/horizon/resources/publish-app-metadata/) を確認する。

### テスト配布

1. Dashboardの `Distribution > Release Channels` を開く。
2. ALPHAへ開発者ビルドをアップロードする。
3. テスターをメール、エイリアス、招待URLで追加する。
4. 問題がなければBETA、次にRCへ昇格する。
5. 各チャンネルでもパッケージ要件を満たすRelease署名ビルドを使う。

Release Channelsの仕様は [Meta公式Release Channels](https://developers.meta.com/horizon/resources/publish-release-channels/) に従う。

### 本申請

1. Productionチャンネルへ最終ビルドをコピーする。
2. DashboardのApp Submissionで全項目を入力する。
3. 価格、対応言語、コンテンツレーティング、Storeアセットを確認する。
4. VRC（Virtual Reality Checks）とTechnical／Content／Data Protection要件を再確認する。
5. 警告とエラーを解消し、すべての必須項目にチェックが付いた状態にする。
6. `Submit for Review` を押す。
7. 審査期間を見込み、公開希望日の少なくとも2週間前に提出する。
8. 承認後、即時公開または公開日を設定する。

公式の申請手順は [Submitting your app](https://developers.meta.com/horizon/resources/publish-submit/) を参照する。審査前に本番ビルドやメタデータを変更すると差分管理が難しくなるため、RCで最終確認してからProductionへコピーする。

## 現在のリポジトリの状態

現状は試作APK向けで、Store申請には以下が未設定である。

- 実在のPackage NameとApp ID
- 署名用Keystore
- Entitlement Check（有料販売する場合）
- Store用アセット、プライバシーポリシー、コンテンツレーティング
- Android向けOpenXR設定の最終確認

本番申請時にこれらを埋め、ALPHA → BETA → RC → Productionの順で進める。
