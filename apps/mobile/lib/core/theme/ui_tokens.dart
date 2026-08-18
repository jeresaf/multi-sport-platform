import 'package:flutter/material.dart';

/// Flutter projection of packages/design-tokens/tokens.json.
abstract final class UiTokens {
  static const mobileMaxWidth = 599.0;
  static const tabletShortestSide = 600.0;
  static const desktopWidth = 1200.0;

  static const space1 = 4.0;
  static const space2 = 8.0;
  static const space3 = 12.0;
  static const space4 = 16.0;
  static const space6 = 24.0;
  static const space8 = 32.0;

  static const radiusSmall = 6.0;
  static const radiusMedium = 10.0;
  static const radiusLarge = 16.0;
  static const radiusExtraLarge = 24.0;

  static const statusLive = Color(0xFFDC2626);
  static const statusUpcoming = Color(0xFF2563EB);
  static const statusFinished = Color(0xFF15803D);
  static const statusPostponed = Color(0xFFB45309);
  static const statusCancelled = Color(0xFF64748B);
}
