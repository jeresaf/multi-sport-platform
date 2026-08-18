import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sports_platform_mobile/core/theme/app_theme.dart';
import 'package:sports_platform_mobile/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:sports_platform_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:sports_platform_mobile/features/auth/presentation/providers/auth_provider.dart';
import 'package:sports_platform_mobile/features/branding/domain/tenant_branding.dart';
import 'package:sports_platform_mobile/features/branding/presentation/providers/branding_provider.dart';
import 'package:sports_platform_mobile/features/home/presentation/screens/home_screen.dart';
import 'package:sports_platform_mobile/features/notifications/presentation/providers/notification_provider.dart';
import 'package:sports_platform_mobile/features/supporter/data/supporter_preferences.dart';
import 'package:sports_platform_mobile/features/supporter/domain/supporter_models.dart';
import 'package:sports_platform_mobile/features/supporter/presentation/providers/supporter_provider.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(_loadDeterministicRoboto);

  final matrix = <({String name, Size size})>[
    (name: 'phone_portrait', size: const Size(390, 844)),
    (name: 'phone_landscape', size: const Size(844, 390)),
    (name: 'tablet_8_portrait', size: const Size(800, 1280)),
    (name: 'tablet_10_portrait', size: const Size(1200, 1920)),
    (name: 'tablet_landscape', size: const Size(1280, 800)),
  ];

  for (final target in matrix) {
    testWidgets('public home golden - ${target.name}', (tester) async {
      await _pumpPublicHome(
        tester,
        size: target.size,
        feed: _populatedFeed(),
      );

      if (target.name == 'phone_landscape') {
        await tester.drag(
          find.byType(CustomScrollView),
          const Offset(0, -220),
        );
        await tester.pumpAndSettle();
      }

      await expectLater(
        find.byKey(_goldenBoundaryKey),
        matchesGoldenFile('goldens/public_home_${target.name}.png'),
      );
    });
  }

  testWidgets('public home golden - empty state', (tester) async {
    await _pumpPublicHome(
      tester,
      size: const Size(390, 844),
      feed: const SupporterFeed(),
    );

    await expectLater(
      find.byKey(_goldenBoundaryKey),
      matchesGoldenFile('goldens/public_home_empty.png'),
    );
  });

  testWidgets('public home golden - error state', (tester) async {
    await _pumpPublicHome(
      tester,
      size: const Size(390, 844),
      error: StateError('The public sports feed could not be reached.'),
    );

    await expectLater(
      find.byKey(_goldenBoundaryKey),
      matchesGoldenFile('goldens/public_home_error.png'),
    );
  });
}

const _goldenBoundaryKey = Key('public-home-golden-boundary');

Future<void> _pumpPublicHome(
  WidgetTester tester, {
  required Size size,
  SupporterFeed? feed,
  Object? error,
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  SharedPreferences.setMockInitialValues({});
  final preferences = await SharedPreferences.getInstance();
  final theme = AppTheme.lightThemeFor(primary: const Color(0xFF0F766E));

  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authStateProvider
            .overrideWith((_) => AuthNotifier(_GuestAuthRepository())),
        tenantBrandingProvider.overrideWith(
          (_) async => const TenantBranding(
            tenantId: 'public-sports',
            tenantName: 'East Africa Sports Hub',
            shortName: 'EASH',
            publicSlug: 'east-africa-sports',
            primaryColor: Color(0xFF0F766E),
          ),
        ),
        unreadNotificationCountProvider.overrideWithValue(0),
        supporterSharedPreferencesProvider.overrideWithValue(preferences),
        supporterFeedProvider.overrideWith(
          () => _GoldenFeedNotifier(feed: feed, error: error),
        ),
      ],
      child: MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: theme.copyWith(
          textTheme: theme.textTheme.apply(fontFamily: 'GoldenRoboto'),
          primaryTextTheme:
              theme.primaryTextTheme.apply(fontFamily: 'GoldenRoboto'),
          appBarTheme: theme.appBarTheme.copyWith(
            titleTextStyle: theme.appBarTheme.titleTextStyle
                ?.copyWith(fontFamily: 'GoldenRoboto'),
          ),
          chipTheme: theme.chipTheme.copyWith(
            labelStyle: theme.chipTheme.labelStyle
                ?.copyWith(fontFamily: 'GoldenRoboto'),
            secondaryLabelStyle: theme.chipTheme.secondaryLabelStyle
                ?.copyWith(fontFamily: 'GoldenRoboto'),
          ),
        ),
        home: const RepaintBoundary(
          key: _goldenBoundaryKey,
          child: HomeScreen(),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _loadDeterministicRoboto() async {
  const fontDirectory = [
    'bin',
    'cache',
    'artifacts',
    'material_fonts',
  ];
  final separator = Platform.pathSeparator;
  final candidates = <Directory>[];
  final configuredRoot = Platform.environment['FLUTTER_ROOT'];
  if (configuredRoot != null) candidates.add(Directory(configuredRoot));

  var ancestor = File(Platform.resolvedExecutable).parent;
  for (var level = 0; level < 10; level++) {
    candidates.add(ancestor);
    ancestor = ancestor.parent;
  }

  for (final root in candidates) {
    final directory = [root.path, ...fontDirectory].join(separator);
    final roboto = File('$directory${separator}roboto-regular.ttf');
    final icons = File('$directory${separator}materialicons-regular.otf');
    if (!roboto.existsSync() || !icons.existsSync()) continue;
    final bytes = await roboto.readAsBytes();
    await (FontLoader('GoldenRoboto')
          ..addFont(Future.value(ByteData.sublistView(bytes))))
        .load();
    final iconBytes = await icons.readAsBytes();
    await (FontLoader('MaterialIcons')
          ..addFont(Future.value(ByteData.sublistView(iconBytes))))
        .load();
    return;
  }
  throw StateError('Flutter SDK golden-test fonts were not found.');
}

SupporterFeed _populatedFeed() {
  final day = DateTime.now();
  DateTime at(int hour, [int minute = 0]) =>
      DateTime(day.year, day.month, day.day, hour, minute);

  return SupporterFeed(
    fixtures: [
      _fixture(
        id: 'rugby-15-live',
        competitionId: 'rugby-premiership',
        competitionName: 'East Africa Rugby Premiership',
        sportCode: 'rugby15',
        sportName: 'Rugby 15s',
        status: 'InProgress',
        startTime: at(15),
        first: 'Kampala Rhinos',
        second: 'Nairobi Lions',
        firstScore: '24',
        secondScore: '19',
      ),
      _fixture(
        id: 'rugby-15-scheduled',
        competitionId: 'rugby-premiership',
        competitionName: 'East Africa Rugby Premiership',
        sportCode: 'rugby15',
        sportName: 'Rugby 15s',
        status: 'Scheduled',
        startTime: at(17, 30),
        first: 'Jinja Nile RFC',
        second: 'Mombasa Mariners',
      ),
      _fixture(
        id: 'rugby-15-final',
        competitionId: 'rugby-premiership',
        competitionName: 'East Africa Rugby Premiership',
        sportCode: 'rugby15',
        sportName: 'Rugby 15s',
        status: 'Completed',
        startTime: at(11),
        first: 'Kigali Silverbacks',
        second: 'Arusha Mountaineers',
        firstScore: '31',
        secondScore: '27',
      ),
      _fixture(
        id: 'rugby-7-live',
        competitionId: 'rugby-sevens',
        competitionName: 'Great Lakes Rugby Sevens',
        sportCode: 'rugby7',
        sportName: 'Rugby 7s',
        status: 'InProgress',
        startTime: at(13, 20),
        first: 'Entebbe Waves',
        second: 'Kisumu Lakeside',
        firstScore: '14',
        secondScore: '12',
      ),
      _fixture(
        id: 'football-scheduled',
        competitionId: 'regional-football',
        competitionName: 'Regional Champions Football League',
        sportCode: 'football',
        sportName: 'Football',
        status: 'Scheduled',
        startTime: at(19),
        first: 'Kampala City Stars',
        second: 'Nakuru Highlanders',
      ),
      _fixture(
        id: 'basketball-final',
        competitionId: 'city-basketball',
        competitionName: 'East Africa City Basketball Series',
        sportCode: 'basketball',
        sportName: 'Basketball',
        status: 'Published',
        startTime: at(10),
        first: 'Dar City Hoops',
        second: 'Kigali Falcons',
        firstScore: '88',
        secondScore: '82',
      ),
    ],
  );
}

SupporterFixture _fixture({
  required String id,
  required String competitionId,
  required String competitionName,
  required String sportCode,
  required String sportName,
  required String status,
  required DateTime startTime,
  required String first,
  required String second,
  String firstScore = '—',
  String secondScore = '—',
}) =>
    SupporterFixture(
      id: id,
      competitionId: competitionId,
      competitionName: competitionName,
      name: '$first vs $second',
      sportCode: sportCode,
      sportName: sportName,
      status: status,
      startTime: startTime,
      venue: 'Community Sports Ground',
      participants: [first, second],
      participantDetails: [
        SupporterFixtureParticipant(
          id: '$id-first',
          name: first,
          scoreDisplay: firstScore,
        ),
        SupporterFixtureParticipant(
          id: '$id-second',
          name: second,
          scoreDisplay: secondScore,
        ),
      ],
    );

class _GoldenFeedNotifier extends SupporterFeedNotifier {
  _GoldenFeedNotifier({this.feed, this.error});

  final SupporterFeed? feed;
  final Object? error;

  @override
  Future<SupporterFeed> build() async {
    if (error != null) throw error!;
    return feed ?? const SupporterFeed();
  }
}

class _GuestAuthRepository implements AuthRepository {
  @override
  Future<User?> getCurrentUser() async => null;

  @override
  Future<bool> isAuthenticated() async => false;

  @override
  Future<User?> login(String email, String password) async => null;

  @override
  Future<void> logout() async {}

  @override
  Future<void> refreshToken() async {}

  @override
  Future<User?> register(
    String email,
    String password,
    String displayName,
  ) async =>
      null;

  @override
  Future<void> sendPasswordReset(String email) async {}
}
