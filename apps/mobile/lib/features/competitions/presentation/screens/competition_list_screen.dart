import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../providers/competition_provider.dart';
import '../../data/repositories/competition_repository_impl.dart';

/// Competition list screen
class CompetitionListScreen extends ConsumerStatefulWidget {
  const CompetitionListScreen({super.key});

  @override
  ConsumerState<CompetitionListScreen> createState() =>
      _CompetitionListScreenState();
}

class _CompetitionListScreenState extends ConsumerState<CompetitionListScreen> {
  final TextEditingController _searchController = TextEditingController();
  String _searchQuery = '';

  @override
  void initState() {
    super.initState();
    // Load competitions on init
    Future.microtask(() {
      ref.read(competitionsProvider.notifier).loadCompetitions();
    });
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _onSearchChanged(String query) {
    setState(() {
      _searchQuery = query.toLowerCase();
    });
    // Debounce search to avoid too many API calls
    Future.delayed(const Duration(milliseconds: 500), () {
      if (_searchQuery == query.toLowerCase()) {
        ref
            .read(competitionsProvider.notifier)
            .setSearchQuery(query.isEmpty ? null : query);
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final competitionsState = ref.watch(competitionsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Competitions'),
        actions: [
          IconButton(
            icon: const Icon(Icons.filter_list),
            onPressed: () {
              _showFilterSheet(context);
            },
          ),
        ],
      ),
      body: Column(
        children: [
          // Search bar
          Padding(
            padding: const EdgeInsets.all(16),
            child: TextField(
              controller: _searchController,
              onChanged: _onSearchChanged,
              decoration: InputDecoration(
                hintText: 'Search competitions...',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchQuery.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _searchController.clear();
                          _onSearchChanged('');
                        },
                      )
                    : null,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: Colors.grey.shade100,
              ),
            ),
          ),

          // Competitions list
          Expanded(
            child: RefreshIndicator(
              onRefresh: () async {
                await ref
                    .read(competitionsProvider.notifier)
                    .loadCompetitions(refresh: true);
              },
              child: competitionsState.isLoading &&
                      competitionsState.competitions.isEmpty
                  ? const Center(child: CircularProgressIndicator())
                  : competitionsState.error != null &&
                          competitionsState.competitions.isEmpty
                      ? _ErrorView(
                          error: competitionsState.error!,
                          onRetry: () {
                            ref
                                .read(competitionsProvider.notifier)
                                .loadCompetitions(refresh: true);
                          },
                        )
                      : competitionsState.competitions.isEmpty
                          ? const _EmptyView()
                          : _buildCompetitionsList(
                              competitionsState.competitions),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCompetitionsList(List<Competition> competitions) {
    // Group competitions by status
    final activeCompetitions = competitions
        .where((c) =>
            c.status == 'InProgress' ||
            c.status == 'Published' ||
            c.status == 'RegistrationOpen')
        .toList();

    final upcomingCompetitions = competitions
        .where((c) => c.status == 'Scheduled' || c.status == 'Draft')
        .toList();

    final completedCompetitions = competitions
        .where((c) => c.status == 'Completed' || c.status == 'Cancelled')
        .toList();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (activeCompetitions.isNotEmpty) ...[
          Text(
            'Active Competitions',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          ...activeCompetitions.map((competition) => _CompetitionCard(
                competition: competition,
                onTap: () => context.push('/competitions/${competition.id}'),
              )),
          const SizedBox(height: 24),
        ],
        if (upcomingCompetitions.isNotEmpty) ...[
          Text(
            'Upcoming',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          ...upcomingCompetitions.map((competition) => _CompetitionCard(
                competition: competition,
                onTap: () => context.push('/competitions/${competition.id}'),
              )),
          const SizedBox(height: 24),
        ],
        if (completedCompetitions.isNotEmpty) ...[
          Text(
            'Past Competitions',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          ...completedCompetitions.map((competition) => _CompetitionCard(
                competition: competition,
                onTap: () => context.push('/competitions/${competition.id}'),
              )),
        ],
      ],
    );
  }

  void _showFilterSheet(BuildContext context) {
    showModalBottomSheet(
      context: context,
      builder: (context) => const _FilterSheet(),
    );
  }
}

class _CompetitionCard extends StatelessWidget {
  final Competition competition;
  final VoidCallback onTap;

  const _CompetitionCard({
    required this.competition,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    Color statusColor;
    switch (competition.status) {
      case 'InProgress':
        statusColor = Colors.red;
        break;
      case 'Published':
      case 'RegistrationOpen':
        statusColor = Colors.green;
        break;
      case 'Scheduled':
      case 'Draft':
        statusColor = Colors.blue;
        break;
      case 'Completed':
        statusColor = Colors.grey;
        break;
      case 'Cancelled':
        statusColor = Colors.red.shade300;
        break;
      default:
        statusColor = Colors.grey;
    }

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      competition.name,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: statusColor.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      competition.status,
                      style: TextStyle(
                        color: statusColor,
                        fontSize: 12,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Icon(Icons.sports, size: 16, color: Colors.grey.shade600),
                  const SizedBox(width: 4),
                  Text(
                    competition.sportCode.toUpperCase(),
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: Colors.grey.shade600,
                        ),
                  ),
                  const SizedBox(width: 16),
                  Icon(Icons.people, size: 16, color: Colors.grey.shade600),
                  const SizedBox(width: 4),
                  Text(
                    '${competition.participantCount} participants',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: Colors.grey.shade600,
                        ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FilterSheet extends ConsumerWidget {
  const _FilterSheet();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitionsState = ref.watch(competitionsProvider);

    return Container(
      padding: const EdgeInsets.all(16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Filter Competitions',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
              ),
              TextButton(
                onPressed: () {
                  ref.read(competitionsProvider.notifier).clearFilters();
                  Navigator.pop(context);
                },
                child: const Text('Clear All'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text(
            'Sport',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w600,
                ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              _FilterChip(
                label: 'All',
                isSelected: competitionsState.selectedSportFilter == null,
                onTap: () {
                  ref.read(competitionsProvider.notifier).setSportFilter(null);
                },
              ),
              ...sportFilters.map((filter) => _FilterChip(
                    label: filter['name'] as String,
                    isSelected:
                        competitionsState.selectedSportFilter == filter['code'],
                    onTap: () {
                      ref
                          .read(competitionsProvider.notifier)
                          .setSportFilter(filter['code'] as String);
                    },
                  )),
            ],
          ),
          const SizedBox(height: 16),
          Text(
            'Status',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w600,
                ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              _FilterChip(
                label: 'All',
                isSelected: competitionsState.selectedStatusFilter == null,
                onTap: () {
                  ref.read(competitionsProvider.notifier).setStatusFilter(null);
                },
              ),
              ...statusFilters.map((filter) => _FilterChip(
                    label: filter['name'] as String,
                    isSelected: competitionsState.selectedStatusFilter ==
                        filter['value'],
                    onTap: () {
                      ref
                          .read(competitionsProvider.notifier)
                          .setStatusFilter(filter['value'] as String);
                    },
                  )),
            ],
          ),
          const SizedBox(height: 24),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Apply Filters'),
            ),
          ),
          const SizedBox(height: 16),
        ],
      ),
    );
  }
}

class _FilterChip extends StatelessWidget {
  final String label;
  final bool isSelected;
  final VoidCallback onTap;

  const _FilterChip({
    required this.label,
    required this.isSelected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        decoration: BoxDecoration(
          color: isSelected
              ? Theme.of(context).colorScheme.primary
              : Colors.grey.shade100,
          borderRadius: BorderRadius.circular(20),
        ),
        child: Text(
          label,
          style: TextStyle(
            color: isSelected ? Colors.white : Colors.grey.shade700,
            fontWeight: isSelected ? FontWeight.w600 : FontWeight.normal,
          ),
        ),
      ),
    );
  }
}

class _EmptyView extends StatelessWidget {
  const _EmptyView();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.emoji_events_outlined,
              size: 64,
              color: Colors.grey.shade400,
            ),
            const SizedBox(height: 16),
            Text(
              'No competitions found',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              'Try adjusting your filters or check back later',
              style: TextStyle(color: Colors.grey.shade600),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  final String error;
  final VoidCallback onRetry;

  const _ErrorView({
    required this.error,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.error_outline,
              size: 64,
              color: Colors.red.shade300,
            ),
            const SizedBox(height: 16),
            Text(
              'Error loading competitions',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              error,
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.grey.shade600),
            ),
            const SizedBox(height: 24),
            ElevatedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}
