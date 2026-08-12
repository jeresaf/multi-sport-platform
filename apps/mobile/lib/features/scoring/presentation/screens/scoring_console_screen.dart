import 'package:flutter/material.dart';

import 'connected_scoring_screen.dart';

class ScoringConsoleScreen extends StatelessWidget {
  final String contestId;
  final String? sportCode;

  const ScoringConsoleScreen({
    super.key,
    required this.contestId,
    this.sportCode,
  });

  @override
  Widget build(BuildContext context) => ConnectedScoringScreen(
        contestId: contestId,
        sportCode: sportCode,
      );
}
