import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/services/offline_sync_service.dart';
import '../../../../core/services/scoring_service.dart';
import '../../../../core/services/service_locator.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../contests/presentation/providers/contest_provider.dart';

final scoringServiceProvider =
    Provider<ScoringService>((ref) => ServiceLocator.get<ScoringService>());

final offlineSyncServiceProvider = Provider<OfflineSyncService>(
    (ref) => ServiceLocator.get<OfflineSyncService>());

class ConnectedScoringScreen extends ConsumerStatefulWidget {
  final String contestId;
  final String? sportCode;

  const ConnectedScoringScreen({
    super.key,
    required this.contestId,
    this.sportCode,
  });

  @override
  ConsumerState<ConnectedScoringScreen> createState() =>
      _ConnectedScoringScreenState();
}

class _ConnectedScoringScreenState
    extends ConsumerState<ConnectedScoringScreen> {
  bool _busy = false;
  bool _sessionActive = false;
  bool _sessionPaused = false;
  bool _clockRunning = false;
  Timer? _clockTimer;
  ScoringSchema? _schema;
  String? _schemaError;
  String? _selectedSegmentId;
  num _clockSeconds = 0;
  final List<ScoringEvent> _localEvents = [];
  final Set<String> _locallyCheckedIn = {};

  @override
  void initState() {
    super.initState();
    _loadSchema();
    _loadSession();
  }

  Future<void> _loadSchema() async {
    try {
      final schema =
          await ref.read(scoringServiceProvider).getSchema(widget.contestId);
      if (mounted) {
        setState(() {
          _schema = schema;
          if (!schema.segments
              .any((segment) => segment.id == _selectedSegmentId)) {
            final leafSegments = schema.segments
                .where(
                  (candidate) => !schema.segments.any(
                    (segment) => segment.parentSegmentId == candidate.id,
                  ),
                )
                .toList(growable: false);
            _selectedSegmentId =
                leafSegments.isEmpty ? null : leafSegments.first.id;
          }
        });
      }
    } catch (error) {
      if (mounted) {
        setState(
            () => _schemaError = 'Unable to load scoring controls: $error');
      }
    }
  }

  Future<void> _loadSession() async {
    try {
      final session =
          await ref.read(scoringServiceProvider).getSession(widget.contestId);
      if (!mounted) return;
      setState(() {
        _sessionActive = session?.status == 1 || session?.status == 2;
        _sessionPaused = session?.status == 2;
        _clockRunning = session?.isClockRunning ?? false;
        if (session?.activeSegmentId != null) {
          _selectedSegmentId = session!.activeSegmentId;
        }
        if (session != null) _clockSeconds = session.currentClockSeconds;
      });
      _syncClockTimer();
    } catch (_) {
      // A missing or temporarily unavailable session does not prevent scoring
      // schema and contest data from loading.
    }
  }

  void _syncClockTimer() {
    _clockTimer?.cancel();
    if (!_clockRunning) return;
    _clockTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() {
        if (_schema?.clockCountsDown == true) {
          _clockSeconds = (_clockSeconds - 1).clamp(0, double.infinity);
        } else {
          _clockSeconds += 1;
        }
      });
    });
  }

  @override
  void dispose() {
    _clockTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authStateProvider);
    if (!auth.isAuthenticated) {
      return Scaffold(
        appBar: AppBar(title: const Text('Scoring')),
        body: const Center(
          child: Padding(
            padding: EdgeInsets.all(24),
            child: Text(
              'Sign in with an authorized scorer account before opening a scoring console.',
              textAlign: TextAlign.center,
            ),
          ),
        ),
      );
    }
    final state = ref.watch(contestDetailProvider(widget.contestId));
    final config = _schema;

    return Scaffold(
      appBar: AppBar(
        title: Text('${config?.name ?? 'Sport'} scoring'),
        actions: [
          IconButton(
            tooltip: 'Review sync issues',
            onPressed: _showReviewQueue,
            icon: const Icon(Icons.sync_problem),
          ),
          IconButton(
            tooltip: 'Refresh',
            onPressed: () {
              ref
                  .read(contestDetailProvider(widget.contestId).notifier)
                  .refresh();
              _loadSchema();
              _loadSession();
            },
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: state.isLoading
          ? const Center(child: CircularProgressIndicator())
          : state.error != null
              ? _ErrorState(message: state.error!)
              : _schemaError != null
                  ? _ErrorState(message: _schemaError!)
                  : config == null
                      ? const Center(child: CircularProgressIndicator())
                      : state.contest == null
                          ? const Center(child: Text('Contest not found.'))
                          : _buildContest(context, state, config),
    );
  }

  Widget _buildContest(
    BuildContext context,
    ContestDetailState state,
    ScoringSchema config,
  ) {
    final contest = state.contest!;
    final participants = contest.participants;
    final totals = <String, Map<String, num>>{};
    for (final event in state.events) {
      final participantId = event.participantId;
      if (participantId == null) continue;
      final value = event.data?['value'];
      if (value is! num) continue;
      final metrics = totals.putIfAbsent(participantId, () => {});
      metrics[event.eventType] = (metrics[event.eventType] ?? 0) + value;
    }
    for (final event in _localEvents.where((event) => !event.isSynced)) {
      final metrics = totals.putIfAbsent(event.participantId, () => {});
      metrics[event.metricName] =
          (metrics[event.metricName] ?? 0) + event.value;
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(contest.name,
                    style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 4),
                Text('${contest.status} · ${config.name}'),
                const SizedBox(height: 14),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    if (!_sessionActive)
                      FilledButton.icon(
                        onPressed: _busy ? null : _startSession,
                        icon: const Icon(Icons.play_arrow),
                        label: const Text('Start session'),
                      ),
                    if (_sessionActive && !_sessionPaused)
                      OutlinedButton.icon(
                        onPressed: _busy ? null : _pauseSession,
                        icon: const Icon(Icons.pause),
                        label: const Text('Pause'),
                      ),
                    if (_sessionPaused)
                      FilledButton.icon(
                        onPressed: _busy ? null : _resumeSession,
                        icon: const Icon(Icons.play_arrow),
                        label: const Text('Resume'),
                      ),
                    if (_sessionActive)
                      OutlinedButton.icon(
                        onPressed: _busy ? null : _endSession,
                        icon: const Icon(Icons.stop),
                        label: const Text('End'),
                      ),
                    OutlinedButton.icon(
                      onPressed: _busy ? null : _submitResults,
                      icon: const Icon(Icons.publish),
                      label: const Text('Submit results'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DropdownButtonFormField<String>(
                  value: _selectedSegmentId,
                  decoration: const InputDecoration(labelText: 'Segment'),
                  items: [
                    for (final segment in config.segments)
                      DropdownMenuItem(
                        value: segment.id,
                        enabled: !config.segments.any(
                          (candidate) =>
                              candidate.parentSegmentId == segment.id,
                        ),
                        child: Text(
                          '${List.filled(segment.depth, '— ').join()}'
                          '${segment.name}'
                          '${segment.isOptional ? ' (optional)' : ''}',
                        ),
                      ),
                  ],
                  onChanged: _busy || !_sessionActive
                      ? null
                      : (value) => _updateContext(segmentId: value),
                ),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: _busy || !_sessionActive ? null : _advanceSegment,
                  icon: const Icon(Icons.skip_next),
                  label: const Text('Next segment'),
                ),
                if (config.supportsClock) ...[
                  const SizedBox(height: 12),
                  TextFormField(
                    key: ValueKey('clock-$_clockSeconds-$_clockRunning'),
                    initialValue: _clockSeconds.toString(),
                    enabled: !_busy && _sessionActive && !_clockRunning,
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: true),
                    decoration: InputDecoration(
                      labelText: config.clockCountsDown
                          ? 'Clock seconds remaining'
                          : 'Clock seconds elapsed',
                    ),
                    onChanged: (value) =>
                        _clockSeconds = num.tryParse(value) ?? 0,
                    onFieldSubmitted: (value) => _updateContext(
                      clockSeconds: num.tryParse(value) ?? 0,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: [
                      FilledButton.icon(
                        onPressed: _busy ||
                                !_sessionActive ||
                                _sessionPaused ||
                                _clockRunning
                            ? null
                            : _startClock,
                        icon: const Icon(Icons.timer),
                        label: const Text('Start clock'),
                      ),
                      OutlinedButton.icon(
                        onPressed: _busy || !_clockRunning ? null : _pauseClock,
                        icon: const Icon(Icons.timer_off),
                        label: const Text('Pause clock'),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        if (participants.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(24),
              child: Text('This contest has no participants to score.'),
            ),
          ),
        for (var index = 0; index < participants.length; index++)
          _participantCard(
            context,
            participants[index],
            index,
            totals,
            config,
          ),
        if (_canCorrect && state.events.isNotEmpty)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Recent events',
                      style: Theme.of(context).textTheme.titleMedium),
                  for (final event in state.events.reversed.take(10))
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      title: Text(event.eventType),
                      subtitle: Text(
                        '${event.data?['value'] ?? 0}'
                        '${event.data?['clockSeconds'] == null ? '' : ' · ${event.data?['clockSeconds']}s'}',
                      ),
                      trailing: PopupMenuButton<String>(
                        onSelected: (action) => action == 'correct'
                            ? _correctEvent(event)
                            : _reverseEvent(event),
                        itemBuilder: (_) => const [
                          PopupMenuItem(
                              value: 'correct', child: Text('Correct')),
                          PopupMenuItem(
                              value: 'reverse', child: Text('Reverse')),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ),
        if (_localEvents.any((event) => !event.isSynced))
          const Padding(
            padding: EdgeInsets.only(top: 8),
            child: Text(
              'Unsynced events are included in the displayed totals.',
              textAlign: TextAlign.center,
            ),
          ),
      ],
    );
  }

  bool get _canCorrect {
    final roles = ref.read(authStateProvider).roles;
    return roles.contains('Admin') || roles.contains('Platform Owner');
  }

  Future<void> _correctEvent(ContestEvent event) async {
    final valueController =
        TextEditingController(text: '${event.data?['value'] ?? 0}');
    final reasonController = TextEditingController();
    final correction = await showDialog<(num, String)>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Correct event'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: valueController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(labelText: 'Corrected value'),
            ),
            TextField(
              controller: reasonController,
              decoration: const InputDecoration(labelText: 'Reason'),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              final value = num.tryParse(valueController.text);
              final reason = reasonController.text.trim();
              if (value != null && reason.isNotEmpty) {
                Navigator.pop(dialogContext, (value, reason));
              }
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
    valueController.dispose();
    reasonController.dispose();
    if (correction == null || !mounted) return;

    final auth = ref.read(authStateProvider);
    setState(() => _busy = true);
    final success = await ref.read(scoringServiceProvider).correctEvent(
          contestId: widget.contestId,
          eventId: event.id,
          newValue: correction.$1,
          reason: correction.$2,
          updatedBy: auth.email,
        );
    if (!mounted) return;
    setState(() => _busy = false);
    _message(
        success ? 'Correction submitted for approval.' : 'Correction failed.',
        error: !success);
    if (success) {
      await ref
          .read(contestDetailProvider(widget.contestId).notifier)
          .refresh();
    }
  }

  Future<void> _reverseEvent(ContestEvent event) async {
    final controller = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Reverse event'),
        content: TextField(
          controller: controller,
          decoration: const InputDecoration(labelText: 'Reason'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              if (controller.text.trim().isNotEmpty) {
                Navigator.pop(dialogContext, controller.text.trim());
              }
            },
            child: const Text('Reverse'),
          ),
        ],
      ),
    );
    controller.dispose();
    if (reason == null || !mounted) return;

    final auth = ref.read(authStateProvider);
    setState(() => _busy = true);
    final success = await ref.read(scoringServiceProvider).reverseEvent(
          contestId: widget.contestId,
          eventId: event.id,
          reason: reason,
          reversedBy: auth.email,
        );
    if (!mounted) return;
    setState(() => _busy = false);
    _message(success ? 'Reversal submitted for approval.' : 'Reversal failed.',
        error: !success);
    if (success) {
      await ref
          .read(contestDetailProvider(widget.contestId).notifier)
          .refresh();
    }
  }

  Widget _participantCard(
    BuildContext context,
    Map<String, dynamic> participant,
    int index,
    Map<String, Map<String, num>> totals,
    ScoringSchema config,
  ) {
    final id = participant['participantId']?.toString() ?? '';
    final metrics = totals[id] ?? const <String, num>{};
    final score = metrics[config.primaryMetric] ?? 0;
    final rawStatus = participant['status'];
    final persistedStatus = rawStatus is int
        ? const ['Entered', 'CheckedIn', 'Withdrawn', 'Disqualified']
            .elementAtOrNull(rawStatus)
        : rawStatus?.toString();
    final checkedIn =
        persistedStatus == 'CheckedIn' || _locallyCheckedIn.contains(id);
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              participant['name']?.toString() ?? 'Participant ${index + 1}',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            Row(
              children: [
                Chip(
                  visualDensity: VisualDensity.compact,
                  label: Text(
                      checkedIn ? 'Checked in' : persistedStatus ?? 'Entered'),
                ),
                const SizedBox(width: 8),
                if (!checkedIn &&
                    persistedStatus != 'Withdrawn' &&
                    persistedStatus != 'Disqualified')
                  OutlinedButton.icon(
                    onPressed: _busy || id.isEmpty ? null : () => _checkIn(id),
                    icon: const Icon(Icons.how_to_reg),
                    label: const Text('Check in'),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              '$score ${config.scoreLabel}',
              style: Theme.of(context).textTheme.headlineMedium,
            ),
            if (config.lowerIsBetter)
              const Text('Lower is better', style: TextStyle(fontSize: 12)),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final action in config.actions)
                  FilledButton.tonal(
                    onPressed:
                        _busy || id.isEmpty ? null : () => _submit(id, action),
                    child: Text(action.label),
                  ),
              ],
            ),
            if (metrics.isNotEmpty) ...[
              const SizedBox(height: 10),
              Text(metrics.entries
                  .map((entry) => '${entry.key}: ${entry.value}')
                  .join(' · ')),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _submit(String participantId, ScoringAction action) async {
    final value = action.inputType == 'number'
        ? await _requestValue(action)
        : action.defaultValue;
    if (value == null || !mounted) return;
    setState(() => _busy = true);
    final auth = ref.read(authStateProvider);
    final result = await ref.read(scoringServiceProvider).submitEvent(
          contestId: widget.contestId,
          participantId: participantId,
          metricName: action.metric,
          value: value,
          scoredBy: auth.email,
          segmentId: _selectedSegmentId,
          clockSeconds: _schema?.supportsClock == true ? _clockSeconds : null,
          eventType: action.eventType,
        );
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (result.event != null) _localEvents.add(result.event!);
    });
    _message(
      result.isSuccess
          ? result.queuedOffline
              ? '${action.label} saved offline and queued in order.'
              : '${action.label} saved.'
          : result.errorMessage ?? 'Unable to save event.',
      error: !result.isSuccess,
    );
    if (result.isSuccess && !result.queuedOffline) {
      await ref
          .read(contestDetailProvider(widget.contestId).notifier)
          .refresh();
      if (mounted) {
        setState(() => _localEvents.removeWhere((event) => event.isSynced));
      }
    }
  }

  Future<void> _checkIn(String participantId) async {
    setState(() => _busy = true);
    final result = await ref.read(scoringServiceProvider).checkInParticipant(
          contestId: widget.contestId,
          participantId: participantId,
        );
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (result.isSuccess) _locallyCheckedIn.add(participantId);
    });
    _message(
      result.isSuccess
          ? result.queuedOffline
              ? 'Check-in saved offline and queued in order.'
              : 'Participant checked in.'
          : result.errorMessage ?? 'Check-in failed.',
      error: !result.isSuccess,
    );
    if (result.isSuccess && !result.queuedOffline) {
      await ref
          .read(contestDetailProvider(widget.contestId).notifier)
          .refresh();
    }
  }

  Future<void> _submitResults() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Submit results?'),
        content: const Text(
          'The current primary scores will be ranked and sent for approval. '
          'Unsynced scoring events will remain ahead of this submission in the queue.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Submit'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _busy = true);
    final result = await ref
        .read(scoringServiceProvider)
        .submitContestResults(widget.contestId);
    if (!mounted) return;
    setState(() => _busy = false);
    _message(
      result.isSuccess
          ? result.queuedOffline
              ? 'Result submission queued behind pending scoring events.'
              : 'Results submitted for approval.'
          : result.errorMessage ?? 'Result submission failed.',
      error: !result.isSuccess,
    );
  }

  Future<num?> _requestValue(ScoringAction action) async {
    final controller =
        TextEditingController(text: action.defaultValue.toString());
    final value = await showDialog<num>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(action.label),
        content: TextField(
          controller: controller,
          autofocus: true,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: InputDecoration(
            labelText: action.unit == null ? 'Value' : 'Value (${action.unit})',
            helperText:
                'Allowed: ${action.minimum ?? 'any'} to ${action.maximum ?? 'any'}',
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              final parsed = num.tryParse(controller.text);
              if (parsed == null ||
                  (action.minimum != null && parsed < action.minimum!) ||
                  (action.maximum != null && parsed > action.maximum!)) {
                return;
              }
              Navigator.pop(dialogContext, parsed);
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
    controller.dispose();
    return value;
  }

  Future<void> _startSession() async {
    await _sessionChange(
      () => ref.read(scoringServiceProvider).startSession(
            widget.contestId,
            name: 'Mobile ${widget.sportCode ?? ''} scoring',
          ),
      onSuccess: () {},
    );
    await _loadSession();
  }

  Future<void> _pauseSession() async => _sessionChange(
        () => ref.read(scoringServiceProvider).pauseSession(widget.contestId),
        onSuccess: () {
          _sessionPaused = true;
          _clockRunning = false;
          _syncClockTimer();
        },
      );

  Future<void> _resumeSession() async => _sessionChange(
        () => ref.read(scoringServiceProvider).resumeSession(widget.contestId),
        onSuccess: () => _sessionPaused = false,
      );

  Future<void> _endSession() async => _sessionChange(
        () => ref.read(scoringServiceProvider).endSession(widget.contestId),
        onSuccess: () {
          _sessionActive = false;
          _sessionPaused = false;
          _clockRunning = false;
          _syncClockTimer();
        },
      );

  Future<void> _updateContext({
    String? segmentId,
    num? clockSeconds,
  }) async {
    final nextSegment = segmentId ?? _selectedSegmentId;
    final nextClock = clockSeconds ?? _clockSeconds;
    setState(() => _busy = true);
    final success = await ref.read(scoringServiceProvider).setSessionContext(
          contestId: widget.contestId,
          segmentId: nextSegment,
          clockSeconds: nextClock,
        );
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (success) {
        _selectedSegmentId = nextSegment;
        _clockSeconds = nextClock;
      }
    });
    if (!success) _message('Unable to update segment or clock.', error: true);
  }

  Future<void> _startClock() async {
    await _sessionChange(
      () => ref.read(scoringServiceProvider).startClock(widget.contestId),
      onSuccess: () {
        _clockRunning = true;
        _syncClockTimer();
      },
    );
  }

  Future<void> _pauseClock() async {
    await _sessionChange(
      () => ref.read(scoringServiceProvider).pauseClock(widget.contestId),
      onSuccess: () {
        _clockRunning = false;
        _syncClockTimer();
      },
    );
    await _loadSession();
  }

  Future<void> _advanceSegment() async {
    await _sessionChange(
      () => ref.read(scoringServiceProvider).advanceSegment(widget.contestId),
      onSuccess: () {
        _clockRunning = false;
        _syncClockTimer();
      },
    );
    await _loadSchema();
    await _loadSession();
  }

  Future<void> _sessionChange(
    Future<bool> Function() operation, {
    required VoidCallback onSuccess,
  }) async {
    setState(() => _busy = true);
    final success = await operation();
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (success) onSuccess();
    });
    if (!success) _message('Session action failed.', error: true);
  }

  Future<void> _showReviewQueue() async {
    final service = ref.read(offlineSyncServiceProvider);
    final events = await service.getReviewableEvents();
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Sync review'),
        content: SizedBox(
          width: double.maxFinite,
          child: events.isEmpty
              ? const Text('No rejected or conflicting events.')
              : ListView(
                  shrinkWrap: true,
                  children: [
                    for (final event in events)
                      ListTile(
                        title:
                            Text('${event.status} · #${event.sequenceNumber}'),
                        subtitle: Text(event.errorMessage ?? event.endpoint),
                        trailing: TextButton(
                          onPressed: () async {
                            await service.supersedeEvent(event.id);
                            if (dialogContext.mounted) {
                              Navigator.pop(dialogContext);
                            }
                          },
                          child: const Text('Dismiss'),
                        ),
                      ),
                  ],
                ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  void _message(String text, {bool error = false}) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(text),
      backgroundColor: error ? Colors.red : null,
    ));
  }
}

class _ErrorState extends StatelessWidget {
  final String message;
  const _ErrorState({required this.message});

  @override
  Widget build(BuildContext context) => Center(
      child: Padding(padding: const EdgeInsets.all(24), child: Text(message)));
}
