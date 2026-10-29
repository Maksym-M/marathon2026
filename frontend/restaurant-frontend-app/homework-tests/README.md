# Тести домашки

Тести вже написані — ти пишеш код, поки вони не стануть зеленими.
Екран — **кошик Owner** (бічна панель поверх меню) за макетами з `homework-design/`.

| Папка | Для чого | Скільки тестів |
|---|---|---|
| `practice/` | практичне: блок гостя в «Guests' pre-orders» + спільні тестові дані | 13 |
| `level-1/` | рівень 1: верстка кошика (шапка, вкладки, секції, футер) | 8 |
| `level-2/` | рівень 2: сервіс, кошик на даних, Dish Summary, стани, Submit, сторінка «Group Pre-Order» (US21–23) | 6 + 23 + 8 + 22 |
| `level-3/` | рівень 3: синхронізація кожні 15 с | 3 + 12 |
| `level-3-bonus/` | бонус: Remove Guest, Remove seat і зміна страв (свої та гостей) | 4 + 15 + 9 |

## Як користуватись

1. **На початку рівня** скопіюй вміст папки рівня в `src/app/`. Структура папок уже збігається.
   - macOS / Linux, з `frontend/restaurant-frontend-app`:
     ```bash
     cp -R homework-tests/practice/. src/app/
     ```
   - Windows PowerShell:
     ```powershell
     Copy-Item -Recurse -Force homework-tests\practice\* src\app\
     ```
   - або просто перетягни папки в IDE.
2. Запусти тести:
   ```bash
   npm run test:hw
   ```
   Спершу вони **червоні** (або не компілюються — файлів ще немає). Це нормально.
3. Пиши код і запускай `npm run test:hw` після кожного кроку. Зелених ставатиме більше.
4. Рівень готовий, коли всі тести рівня зелені.

Рівні **накопичуються**: файли різних рівнів не перетинаються, тести попередніх рівнів лишаються і мають бути зеленими.
`practice/` приносить `components/group-orders/testing/group-order.fixtures.ts` — дані з макета (Dmytro, Nino, Luka, Giorgi) і хелпери `renderCart`, `buttonByText`, `dialog`, `text`.

## Що тести перевіряють

Тести дивляться на те, що бачить користувач (текст, кнопки, діалоги, `aria-*`):
- компоненти `GuestPreOrderComponent`, `GroupOrderCartComponent` (`components/group-orders/…`), сторінка `GroupOrderPageComponent`, сервіс `GroupOrdersService`;
- константа `GROUP_ORDER_SYNC_MS` у `group-order-cart/group-order.store.ts`;
- блок гостя: input `participant`, `editable`, `canRemove`, `busy`; output `quantityChange`, `removeClick`;
- методи сервісу — `getGroupInvite$` (повертає `{ token, invite }`), `getSessions$`, `getOwnerSessionId$`, `getStatus$`, `submit$`, `removeSession$`, `removeSeat$`, `updateDishQuantity$`; `getSessions$` і `getStatus$` приймають `{ silent: true }`;
- тексти: «Cart», «Participants», «Dish Summary», «My Pre-Order», «Guests' pre-orders», «X of Y submitted», «Updated …», «Not submitted yet», «N seat(s) not joined yet», «Invite Guests», «Grand Total», «Submit Pre-Order», у діалогах — «Submit» / «Submitting…» / «Cancel», «Remove Guest», «Remove», «Remove seat», «Sent to Restaurant», «Try Again»;
- головна пастка: **Grand Total = $87.00** (лише підтверджені кошики), а не `total` з API ($96.50).

Червоний тест — це підказка: прочитай його назву і повідомлення, там написано, чого бракує.
