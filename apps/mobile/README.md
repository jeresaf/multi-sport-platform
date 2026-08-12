# Sports Platform mobile

Flutter client for supporters and event scorers.

Run against the local Docker API:

```powershell
flutter run `
  --dart-define=API_BASE_URL=http://10.0.2.2:8080 `
  --dart-define=PUBLIC_WEB_BASE_URL=http://10.0.2.2:3001
```

`10.0.2.2` is the Android emulator route to the Windows host. For a physical
device, replace it with the computer's LAN address.

The app opens in public supporter mode. Use **Organisation** to enter a tenant
slug (for example `six-nations`); the choice and resolved branding persist on
the device. Login is optional and only unlocks account features. Scoring is
shown only to scorer/admin roles.

Android can be built on Windows with `flutter build apk`. The checked-in iOS
runner is built by the macOS job in `.github/workflows/flutter-ci.yml` using
`flutter build ios --simulator --no-codesign`.

Supporters can save fixtures and competitions from their cards, revisit them
under **Scores → Favourites**, and share tenant-aware public-web links through
the native share sheet. Low-data mode is available from Scores or Settings; it
prefers saved snapshots, pauses the live-contest poller, and skips remote logos.

## Production push notifications

Push remains off in ordinary local builds, so missing cloud credentials never
break development. To produce a Firebase-enabled build:

1. Run `flutterfire configure` for Android and iOS against the production
   Firebase project.
2. Keep the generated `android/app/google-services.json` and
   `ios/Runner/GoogleService-Info.plist` in the secure release pipeline. Do not
   substitute demo credentials.
3. Upload the Apple Push Notification key in Firebase and use an Apple signing
   profile with the Push Notifications capability. FCM delivery must be tested
   on a physical Apple device; the iOS simulator is not sufficient.
4. Build with both feature switches enabled:

```powershell
flutter build apk --release `
  --dart-define=USE_FIREBASE=true `
  --dart-define=USE_FCM=true `
  --dart-define=API_BASE_URL=https://api.example.com `
  --dart-define=PUBLIC_WEB_BASE_URL=https://sports.example.com
```

The app requests permission only after the user enables notifications in
Settings. It registers the refreshed FCM token with the authenticated API,
disables that device before logout or opt-out, subscribes to the selected
tenant and favourite topics, displays foreground messages through the native
notification tray, suppresses duplicate message IDs, and routes notification
taps to the matching contest or competition.

The API sender uses a Firebase service-account JSON file. Mount that file as a
read-only deployment secret and set `Firebase__CredentialsPath` to its path
inside the API container; optionally set `Firebase__ProjectId`. Never commit
the JSON file or put its contents in an environment variable. Without a valid
path the sender deliberately remains disabled, so development and staging can
run without cloud credentials.

The server publishes the same notification ID to the tenant, favourite
contest, and favourite competition topics. This preserves tenant context while
allowing the client to suppress duplicates. Dispatch is connected to contest
start, score changes, period end, approved final results, bracket advancement,
and fixture rescheduling/postponement. Physical Android and iOS receipt still
requires the production Firebase project and Apple push configuration above.
