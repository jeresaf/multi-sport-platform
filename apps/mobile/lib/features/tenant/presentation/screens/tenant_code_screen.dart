import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/router/app_router.dart';
import '../../../branding/presentation/providers/branding_provider.dart';
import '../../../competitions/presentation/providers/competition_provider.dart';
import '../../../contests/presentation/providers/contest_provider.dart';
import '../providers/tenant_provider.dart';

class TenantCodeScreen extends ConsumerStatefulWidget {
  const TenantCodeScreen({super.key});

  @override
  ConsumerState<TenantCodeScreen> createState() => _TenantCodeScreenState();
}

class _TenantCodeScreenState extends ConsumerState<TenantCodeScreen> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _select() async {
    final selected = await ref
        .read(tenantStateProvider.notifier)
        .discoverAndSelect(_controller.text);
    if (!selected || !mounted) return;
    ref.invalidate(tenantBrandingProvider);
    ref.invalidate(competitionsProvider);
    ref.invalidate(liveContestsProvider);
    context.go(Routes.home);
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(tenantStateProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Choose organisation')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text(
            'Follow your organisation',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          const Text(
            'Enter the tenant code from its public web address, for example “six-nations”.',
          ),
          const SizedBox(height: 24),
          TextField(
            controller: _controller,
            autocorrect: false,
            textCapitalization: TextCapitalization.none,
            decoration: const InputDecoration(
              labelText: 'Tenant code',
              prefixIcon: Icon(Icons.apartment),
              border: OutlineInputBorder(),
            ),
            onSubmitted: (_) {
              if (!state.isLoading) _select();
            },
          ),
          if (state.error != null) ...[
            const SizedBox(height: 12),
            Text(
              state.error!.replaceFirst('Bad state: ', ''),
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ],
          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed: state.isLoading ? null : _select,
            icon: state.isLoading
                ? const SizedBox.square(
                    dimension: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.check),
            label: const Text('Use this organisation'),
          ),
          if (state.availableTenants.isNotEmpty) ...[
            const SizedBox(height: 32),
            Text('Recently used',
                style: Theme.of(context).textTheme.titleMedium),
            ...state.availableTenants.map(
              (tenant) => ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.history),
                title: Text(tenant.name),
                subtitle: Text(tenant.slug),
                onTap: () async {
                  await ref
                      .read(tenantStateProvider.notifier)
                      .selectTenant(tenant.id);
                  ref.invalidate(tenantBrandingProvider);
                  ref.invalidate(competitionsProvider);
                  ref.invalidate(liveContestsProvider);
                  if (context.mounted) context.go(Routes.home);
                },
              ),
            ),
          ],
        ],
      ),
    );
  }
}
