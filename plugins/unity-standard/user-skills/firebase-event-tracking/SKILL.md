---
name: firebase-event-tracking
description: Quy tắc studio để gắn analytics/Firebase tracking trong game Unity (NabaGame stack) — 2 class hằng số TrackEvent & TrackParamater (2 file riêng), gọi thẳng TrackingManager.TrackEvent, đúng shape "event = nhóm, param value = chi tiết, KHÔNG nhét chi tiết vào key". Dùng khi thêm/sửa event tracking, dựng funnel, đếm lượt mua/chọn item, đếm bước tutorial, hoặc khi marketing báo "event trên Firebase đọc sai / không tách được".
---

# Firebase / Analytics Event Tracking — Quy tắc chung của studio

Chuẩn hoá cách gắn tracking để marketing đọc được trên Firebase (GA4): dựng funnel, đếm
theo từng item/bước. Áp dụng cho mọi dự án Unity dùng `NabaGame.Tracking.TrackingManager`
(bọc `FirebaseAnalytics.LogEvent`). Skill này viết generic — copy nguyên sang dự án khác dùng được ngay.

## Nguyên tắc VÀNG về shape (đọc trước khi code)

`TrackingManager.TrackEvent(eventName, paramName, paramValue)` cuối cùng gọi
`FirebaseAnalytics.LogEvent(eventName, paramName, paramValue)`. Chia 3 vai trò cố định:

- **`eventName`** = 1 **NHÓM cố định**, số lượng nhỏ (vd `item_bucket`, `play_minigame`,
  `tut_maingame`). GA4 đếm/dựng funnel theo event name.
- **`paramName`** = 1 **KEY ổn định** cho nhóm đó (vd `buy_item`, `select_item`, `complete`,
  `start_game`). Số lượng nhỏ, cố định.
- **`paramValue`** = **CHI TIẾT biến thiên** (tên item, tên bước tutorial). Đây là thứ marketing
  cần tách theo từng giá trị.

> ❌ **SAI (anti-pattern thường gặp):** nhét chi tiết biến thiên vào **event name** hoặc **param KEY**.
> Ví dụ hỏng: `LogEvent("Tutorial", "step_ChooseItem", "1")` (bước nằm ở KEY) hay
> `LogEvent("Buy_Item", <itemName>, "1")` (item nằm ở KEY). Hậu quả: chi tiết sinh ra vô số KEY mới,
> value chỉ còn `"1"` vô nghĩa → không tách/đếm theo item được.
>
> ✅ **ĐÚNG:** `LogEvent("item_bucket", "buy_item", "Some_Item_Name")` — nhóm ở event,
> hành động ở param key, chi tiết ở value.

## Quy ước bắt buộc

1. **Đúng tên class:** `TrackEvent` và `TrackParamater` (giữ đúng chính tả **"Paramater"** — quy
   ước lịch sử của studio, KHÔNG tự sửa thành "Parameter").
2. **Tách 2 FILE riêng** (`TrackEvent.cs`, `TrackParamater.cs`) đặt ở thư mục Util/Ultils của
   game, để ai cũng tìm ra "toàn bộ dữ liệu tracking" ở một chỗ.
   *Thực tế repo này:* cả hai class đang nằm chung trong `Assets/_GameBase/Scripts/_Others/TrackEvent.cs`
   và khai báo `public class` chứ không phải `public static class`. Khi nào sửa file đó thì tách ra cho
   đúng quy ước; đừng viết code mới dựa trên tình trạng hiện tại.
3. Class chỉ chứa **hằng số `public static readonly string`** — thuần data, không logic.
   (Ngoại lệ duy nhất: hàm map enum→hằng-số, xem mục dưới.)
4. **Gọi THẲNG `TrackingManager.TrackEvent(...)`** ở call site. KHÔNG bọc thêm class wrapper
   (kiểu `GameTracking.TrackX()`) che mất `TrackingManager` — người sau phải grep ra được lời gọi thật.
5. **Code trong `Packages/com.nabagame.reward/` KHÔNG được gọi `TrackingManager`.** Package cấm mọi
   dependency ngoài danh sách cho phép (xem skill `reward-package`) -- và không tham chiếu được thật:
   studio ship cùng một API dưới hai tên assembly khác nhau (`com.nabagame.tracking.runtime` ở
   paint-and-seek, `com.bmh.tracking.runtime` ở project tiêu thụ), UPM không resolve nổi dependency
   git-URL khai báo trong `package.json` của một package, và `TrackingManager.trackers` NRE nếu prefab
   singleton chưa Awake.

   **Từ 1.1.0, package tự bắn analytics qua hook `RewardHooks.TrackEvent`** (`Action<string,string,string>`,
   khớp đúng chữ ký `TrackEvent(eventName, paramName, paramValue)` để host gán bằng method group).
   Nghĩa là: hằng số **param key** nằm TRONG package (`RewardTrack`) vì host không thể biết panel bắn
   lúc nào; chỉ **tên event** là chuỗi host điền vào field `trackEventName` trên prefab panel. Host chỉ
   viết một dòng `RewardHooks.TrackEvent = TrackingManager.TrackEvent;` ở composition root.
   Đây là ngoại lệ có chủ đích của câu "hằng số tracking luôn thuộc host" bên trên; mọi code khác
   (game, demo host) vẫn theo đúng quy tắc đó.

## Template copy-paste

**`TrackEvent.cs`**
```csharp
namespace <YourGameNamespace>
{
    // 1 event name cho mỗi NHÓM. Detail đi vào paramValue, không vào đây.
    public static class TrackEvent
    {
        public static readonly string Play          = "play";
        public static readonly string Result        = "result";
        public static readonly string Item_Bucket   = "item_bucket";
        public static readonly string Play_Minigame = "play_minigame";
        public static readonly string Tut_Maingame  = "tut_maingame";
        // ... thêm nhóm mới ở đây
    }
}
```

**`TrackParamater.cs`**
```csharp
namespace <YourGameNamespace>
{
    // 1 param KEY ổn định cho mỗi loại hành động trong nhóm.
    public static class TrackParamater
    {
        public static readonly string Buy_Item    = "buy_item";
        public static readonly string Select_Item = "select_item";
        public static readonly string Complete    = "complete";
        public static readonly string Start_Game  = "start_game";
        public static readonly string End_Game    = "end_game";
        // ... thêm key mới ở đây
    }
}
```

**Call site** (file cần `using NabaGame.Tracking;`)
```csharp
// đếm sự kiện đơn giản
TrackingManager.TrackEvent(TrackEvent.Play_Minigame, TrackParamater.Start_Game, "1");

// đếm theo chi tiết (item / bước) -> chi tiết nằm ở VALUE
TrackingManager.TrackEvent(TrackEvent.Item_Bucket, TrackParamater.Buy_Item, itemName);
TrackingManager.TrackEvent(TrackEvent.Tut_Maingame, TrackParamater.Complete, state.ToString());

// nhiều tham số: dùng overload có sẵn hoặc Parameter[]
var paramater = new Parameter[]
{
    new Parameter(TrackParamater.Mode, mode),
    new Parameter(TrackParamater.Session, session),
};
TrackingManager.TrackEvent(TrackEvent.Play, paramater); // cần #if NB_FIREBASE_ANALYTIC
```

## Quy trình thêm 1 event mới

1. Thêm hằng số **event name** vào `TrackEvent.cs` (nếu là nhóm mới).
2. Thêm hằng số **param name** vào `TrackParamater.cs` (nếu là hành động mới).
3. Tìm **hook point** — nơi hành động thực sự xảy ra (không phải nơi UI mở). Ví dụ:
   - "mua item" → callback **mua thành công** (Gold/Video/IAP), không phải lúc bấm.
   - "hoàn thành bước tutorial N" → nơi state machine **advance qua bước N**.
   - "vào/thoát minigame" → nơi scene minigame thực sự bắt đầu / cổng thoát kích hoạt.
4. Gọi `TrackingManager.TrackEvent(...)` tại hook, chi tiết biến thiên đặt ở **value**.

### Map enum → nhóm (khi nhiều enum dồn về 1 event)
Nếu nhiều giá trị enum gộp về vài nhóm event (vd nhiều category item → vài event bucket): đây là 1
hàm map **static, thuần** — chỉ nhận enum và **chỉ trả về hằng số của `TrackEvent`**, không đọc state,
không gọi `TrackingManager`, không I/O. Nó là "dữ liệu tracking" → đặt **ngay trong `TrackEvent.cs`**
làm 1 `public static` method, cạnh các hằng số. Trả `null` cho loại không track (để call site bỏ qua).

> ❌ **SAI (anti-pattern):** nhét hàm map này thành `static` trên 1 `MonoBehaviour`/class domain
> (một component gameplay). Class đó không static, lại bị class khác gọi xuyên qua tên nó → tiện ích
> tracking bị phân tán, khó grep, phụ thuộc ngược vào 1 component gameplay. Hàm chỉ ánh xạ
> enum → hằng số `TrackEvent` thì KHÔNG có "domain" nào phù hợp hơn chính `TrackEvent`.

```csharp
// trong TrackEvent.cs, cùng namespace, cạnh các hằng số:
public static string GetBucketEvent(SomeCategory category)
{
    switch (category)
    {
        case SomeCategory.A:
        case SomeCategory.B: return Item_Bucket;
        case SomeCategory.C: return Play_Minigame;
        // ...
        default: return null; // không thuộc nhóm nào -> không bắn
    }
}
// call site (bất kỳ class nào):
string ev = TrackEvent.GetBucketEvent(category);
if (!string.IsNullOrEmpty(ev))
    TrackingManager.TrackEvent(ev, TrackParamater.Buy_Item, itemName);
```

## Giữ song song event cũ (khi refactor)
Khi chuẩn hoá lại tracking của game đang chạy, marketing thường muốn **không đứt dữ liệu lịch
sử**. Thêm call mới **ngay cạnh** call cũ (không xoá), chạy song song 1 thời gian rồi mới gỡ call cũ.

## Verify
- **Firebase KHÔNG chạy trong Unity Editor** (`FirebaseTracker` chỉ log khi define
  `NB_FIREBASE_ANALYTIC` + init thành công). Trong Editor: tạm thêm `Debug.Log` tại hook để xem
  đúng `(event, param, value)`, hoặc dựa vào log của TrackingManager.
- **Trên thiết bị / build dev có `NB_FIREBASE_ANALYTIC`:** mở **Firebase DebugView** để thấy event
  realtime. Kiểm tra: event name đúng nhóm, param key đúng, value chứa chi tiết.
- Tên hợp lệ Firebase: event/param name ≤ 40 ký tự, chỉ chữ-số-gạch dưới, bắt đầu bằng chữ; value
  ≤ 100 ký tự; tránh prefix `firebase_` / `google_` / `ga_`.
