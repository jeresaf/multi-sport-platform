import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../auth/presentation/providers/auth_provider.dart';
import '../../data/registration_repository.dart';

class MyRegistrationsScreen extends ConsumerWidget {
  const MyRegistrationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authStateProvider);
    if (!auth.isAuthenticated) {
      return Scaffold(appBar: AppBar(title: const Text('My registrations')), body: Center(child: FilledButton(onPressed: () => context.push('/login'), child: const Text('Sign in to view registrations'))));
    }
    final registrations = ref.watch(myRegistrationsProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('My registrations')),
      body: registrations.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(child: Padding(padding: const EdgeInsets.all(24), child: Column(mainAxisSize: MainAxisSize.min, children: [Text(error.toString(), textAlign: TextAlign.center), const SizedBox(height: 12), FilledButton(onPressed: () => ref.invalidate(myRegistrationsProvider), child: const Text('Try again'))]))),
        data: (items) => RefreshIndicator(
          onRefresh: () async => ref.refresh(myRegistrationsProvider.future),
          child: items.isEmpty
              ? ListView(children: const [SizedBox(height: 160), Icon(Icons.assignment_outlined, size: 56), SizedBox(height: 16), Center(child: Text('No registrations yet'))])
              : ListView.separated(
                  padding: const EdgeInsets.all(16), itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (context, index) {
                    final item = items[index];
                    final editable = item.status == 'Draft' || item.formStatus == 'ChangesRequested';
                    final note = item.reviewNotes ?? item.cancellationReason;
                    return Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Row(crossAxisAlignment: CrossAxisAlignment.start, children: [Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(item.competitionName, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w800)), Text(item.participantName)])), _StatusChip(item.status)]),
                      const SizedBox(height: 12), Text('Form: ${item.formStatus ?? 'Not linked'}'),
                      if (note != null) Padding(padding: const EdgeInsets.only(top: 8), child: Text('Official note: $note')),
                      if (editable) Padding(padding: const EdgeInsets.only(top: 12), child: FilledButton.icon(onPressed: () => context.push('/registration/${item.competitionId}?registrationId=${item.id}'), icon: const Icon(Icons.edit_outlined), label: Text(item.formStatus == 'ChangesRequested' ? 'Make changes' : 'Resume draft'))),
                    ])));
                  },
                ),
        ),
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip(this.status);
  final String status;
  @override
  Widget build(BuildContext context) {
    final color = switch (status) { 'Approved' || 'Confirmed' => Colors.green, 'Rejected' || 'Withdrawn' || 'Cancelled' => Colors.red, 'Draft' => Colors.grey, _ => Colors.orange };
    return Chip(label: Text(status.replaceAllMapped(RegExp(r'([a-z])([A-Z])'), (match) => '${match[1]} ${match[2]}')), backgroundColor: color.withValues(alpha: .12), side: BorderSide.none, labelStyle: TextStyle(color: color.shade700, fontWeight: FontWeight.w700));
  }
}
