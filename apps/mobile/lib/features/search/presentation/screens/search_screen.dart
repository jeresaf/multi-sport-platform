import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/public_search.dart';

class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  static const _types = <String, String>{
    'all': 'All',
    'sport': 'Sports',
    'competition': 'Competitions',
    'team': 'Teams',
    'athlete': 'Athletes',
    'contest': 'Contests',
    'venue': 'Venues',
  };

  Timer? _debounce;
  final _controller = TextEditingController();
  String _typedQuery = '';
  String _query = '';
  String _type = 'all';

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onQueryChanged(String value) {
    setState(() => _typedQuery = value);
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () {
      if (mounted) setState(() => _query = value.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    final searchKey = (term: _query, type: _type);
    final results =
        _query.isEmpty ? null : ref.watch(publicSearchProvider(searchKey));
    return Scaffold(
      appBar: AppBar(title: const Text('Discover')),
      body: Column(
        children: [
          Container(
            width: double.infinity,
            margin: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.surface,
              borderRadius: BorderRadius.circular(20),
              border: Border.all(
                color: Theme.of(context).colorScheme.outlineVariant,
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Find sports, teams & competitions',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                ),
                const SizedBox(height: 10),
                SearchBar(
                  controller: _controller,
                  autoFocus: true,
                  hintText: 'Sports, competitions, teams, athletes…',
                  leading: const Icon(Icons.search),
                  trailing: _typedQuery.isEmpty
                      ? null
                      : [
                          IconButton(
                            tooltip: 'Clear search',
                            onPressed: () => setState(() {
                              _controller.clear();
                              _typedQuery = '';
                              _query = '';
                            }),
                            icon: const Icon(Icons.close),
                          ),
                        ],
                  onChanged: _onQueryChanged,
                ),
              ],
            ),
          ),
          SizedBox(
            height: 48,
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              scrollDirection: Axis.horizontal,
              itemCount: _types.length,
              separatorBuilder: (_, __) => const SizedBox(width: 8),
              itemBuilder: (context, index) {
                final entry = _types.entries.elementAt(index);
                return ChoiceChip(
                  label: Text(entry.value),
                  selected: _type == entry.key,
                  onSelected: (_) => setState(() => _type = entry.key),
                );
              },
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: _query.isEmpty
                ? const _SearchMessage(
                    icon: Icons.manage_search,
                    title: 'Search public sports coverage',
                    message:
                        'Find sports, competitions, teams, athletes, contests, and venues.',
                  )
                : results!.when(
                    loading: () => const Center(
                      child: CircularProgressIndicator(),
                    ),
                    error: (error, _) => _SearchMessage(
                      icon: Icons.cloud_off,
                      title: 'Search unavailable',
                      message: error.toString(),
                      action: FilledButton.icon(
                        onPressed: () =>
                            ref.invalidate(publicSearchProvider(searchKey)),
                        icon: const Icon(Icons.refresh),
                        label: const Text('Try again'),
                      ),
                    ),
                    data: (response) => response.items.isEmpty
                        ? const _SearchMessage(
                            icon: Icons.search_off,
                            title: 'No matches',
                            message: 'Try another name or result type.',
                          )
                        : _SearchResults(response: response),
                  ),
          ),
        ],
      ),
    );
  }
}

class _SearchResults extends StatelessWidget {
  const _SearchResults({required this.response});

  final PublicSearchResponse response;

  @override
  Widget build(BuildContext context) {
    final groups = <String, List<PublicSearchItem>>{};
    for (final item in response.items) {
      groups.putIfAbsent(item.type, () => []).add(item);
    }
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(14),
          ),
          child: Row(
            children: [
              Icon(Icons.manage_search,
                  size: 18, color: Theme.of(context).colorScheme.primary),
              const SizedBox(width: 8),
              Text(
                '${response.total} public results',
                style: Theme.of(context)
                    .textTheme
                    .labelLarge
                    ?.copyWith(fontWeight: FontWeight.w700),
              ),
            ],
          ),
        ),
        for (final group in groups.entries) ...[
          Padding(
            padding: const EdgeInsets.only(top: 20, bottom: 6),
            child:
                Text(group.key, style: Theme.of(context).textTheme.titleMedium),
          ),
          ...group.value.map((item) => Card(
                elevation: 0,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(16),
                  side: BorderSide(
                    color: Theme.of(context).colorScheme.outlineVariant,
                  ),
                ),
                child: ListTile(
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 6,
                  ),
                  leading: Icon(_iconFor(item.type)),
                  title: Text(item.title),
                  subtitle: item.subtitle == null ? null : Text(item.subtitle!),
                  trailing: item.href == null
                      ? item.status == null
                          ? null
                          : Text(item.status!)
                      : const Icon(Icons.chevron_right),
                  onTap: item.href == null
                      ? null
                      : () => context.push(_mobileRoute(item)),
                ),
              )),
        ],
      ],
    );
  }
}

String _mobileRoute(PublicSearchItem item) {
  final href = item.href;
  if (href != null && href.startsWith('/')) return href;
  return switch (item.type.toLowerCase()) {
    'competition' => '/competition/${item.id}',
    'team' => '/team/${item.id}',
    'athlete' => '/athlete/${item.id}',
    'contest' => '/contest/${item.id}',
    _ => '/',
  };
}

IconData _iconFor(String type) => switch (type.toLowerCase()) {
      'sport' => Icons.sports,
      'competition' => Icons.emoji_events_outlined,
      'team' => Icons.groups_outlined,
      'athlete' => Icons.person_outline,
      'contest' => Icons.event_outlined,
      'venue' => Icons.place_outlined,
      _ => Icons.search,
    };

class _SearchMessage extends StatelessWidget {
  const _SearchMessage({
    required this.icon,
    required this.title,
    required this.message,
    this.action,
  });

  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(icon, size: 52, color: Theme.of(context).colorScheme.outline),
            const SizedBox(height: 12),
            Text(title, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(message, textAlign: TextAlign.center),
            if (action != null) ...[
              const SizedBox(height: 20),
              action!,
            ],
          ]),
        ),
      );
}
