import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../../core/services/api_client.dart';
import '../../../../core/services/service_locator.dart';

/// Profile screen
class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  late TextEditingController _nameController;
  late TextEditingController _emailController;
  bool _isEditing = false;

  @override
  void initState() {
    super.initState();
    final authState = ref.read(authStateProvider);
    _nameController = TextEditingController(text: authState.displayName ?? '');
    _emailController = TextEditingController(text: authState.email ?? '');
  }

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authStateProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        actions: [
          if (!_isEditing)
            IconButton(
              icon: const Icon(Icons.edit),
              onPressed: () {
                setState(() {
                  _isEditing = true;
                });
              },
            )
          else
            IconButton(
              icon: const Icon(Icons.check),
              onPressed: _saveProfile,
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // Profile header
          Card(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                children: [
                  Stack(
                    children: [
                      CircleAvatar(
                        radius: 48,
                        backgroundColor: Theme.of(context).colorScheme.primary,
                        child: Text(
                          (authState.displayName ?? authState.email ?? 'U')
                              .substring(0, 1)
                              .toUpperCase(),
                          style: const TextStyle(
                            fontSize: 36,
                            fontWeight: FontWeight.bold,
                            color: Colors.white,
                          ),
                        ),
                      ),
                      Positioned(
                        bottom: 0,
                        right: 0,
                        child: CircleAvatar(
                          radius: 16,
                          backgroundColor:
                              Theme.of(context).colorScheme.secondary,
                          child: IconButton(
                            icon: const Icon(Icons.camera_alt, size: 16),
                            color: Colors.white,
                            onPressed: () {
                              // TODO: Change profile photo
                            },
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Text(
                    authState.displayName ?? 'User',
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    authState.email ?? '',
                    style: TextStyle(color: Colors.grey.shade600),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 24),

          // Profile form
          Text(
            'Personal Information',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Form(
                key: _formKey,
                child: Column(
                  children: [
                    TextFormField(
                      controller: _nameController,
                      enabled: _isEditing,
                      decoration: const InputDecoration(
                        labelText: 'Display Name',
                        prefixIcon: Icon(Icons.person_outline),
                      ),
                      validator: (value) {
                        if (value == null || value.isEmpty) {
                          return 'Please enter your name';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _emailController,
                      enabled: _isEditing,
                      decoration: const InputDecoration(
                        labelText: 'Email',
                        prefixIcon: Icon(Icons.email_outlined),
                      ),
                      validator: (value) {
                        if (value == null || value.isEmpty) {
                          return 'Please enter your email';
                        }
                        if (!value.contains('@')) {
                          return 'Please enter a valid email';
                        }
                        return null;
                      },
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: 24),

          // Account settings
          Text(
            'Account Settings',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.lock_outline),
                  title: const Text('Change Password'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: _showChangePasswordDialog,
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.notifications_outlined),
                  title: const Text('Notification Preferences'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => context.push('/notifications'),
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.security),
                  title: const Text('Security'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => _showAccountInformation(
                    'Security',
                    'Your session is protected with expiring access tokens. Sign out after using a shared device.',
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),

          // Connected accounts
          Text(
            'Connected Accounts',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.link),
                  title: const Text('Link Google Account'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => _showAccountInformation(
                    'Google Account',
                    'Google account linking is not enabled for this deployment.',
                  ),
                ),
                const Divider(height: 1),
                ListTile(
                  leading: const Icon(Icons.link),
                  title: const Text('Link Apple Account'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => _showAccountInformation(
                    'Apple Account',
                    'Apple account linking is not enabled for this deployment.',
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),

          // Danger zone
          Text(
            'Danger Zone',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: Colors.red,
                ),
          ),
          const SizedBox(height: 12),
          Card(
            child: ListTile(
              leading: Icon(Icons.delete_outline, color: Colors.red.shade400),
              title: Text(
                'Delete Account',
                style: TextStyle(color: Colors.red.shade400),
              ),
              onTap: () {
                _showDeleteAccountDialog();
              },
            ),
          ),
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  void _saveProfile() {
    if (_formKey.currentState!.validate()) {
      final auth = ref.read(authStateProvider);
      _nameController.text = auth.displayName ?? '';
      _emailController.text = auth.email ?? '';
      setState(() {
        _isEditing = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Profile editing is not enabled for this deployment'),
        ),
      );
    }
  }

  void _showAccountInformation(String title, String message) {
    showDialog(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  void _showChangePasswordDialog() {
    final current = TextEditingController();
    final next = TextEditingController();
    showDialog(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Change Password'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: current,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Current password'),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: next,
              obscureText: true,
              decoration: const InputDecoration(
                labelText: 'New password (8+ characters)',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () async {
              final messenger = ScaffoldMessenger.of(context);
              if (current.text.length < 8 || next.text.length < 8) {
                messenger.showSnackBar(const SnackBar(
                  content: Text('Both passwords must be at least 8 characters'),
                ));
                return;
              }
              try {
                await ServiceLocator.get<ApiClient>().post<void>(
                  '/api/auth/change-password',
                  data: {
                    'currentPassword': current.text,
                    'newPassword': next.text,
                  },
                );
                if (!dialogContext.mounted) return;
                Navigator.pop(dialogContext);
                messenger.showSnackBar(const SnackBar(
                  content: Text('Password changed successfully'),
                ));
              } catch (_) {
                if (!dialogContext.mounted) return;
                messenger.showSnackBar(const SnackBar(
                  content: Text(
                      'Password change failed. Check the current password.'),
                ));
              }
            },
            child: const Text('Change'),
          ),
        ],
      ),
    ).whenComplete(() {
      current.dispose();
      next.dispose();
    });
  }

  void _showDeleteAccountDialog() {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete Account'),
        content: const Text(
          'Are you sure you want to delete your account? This action cannot be undone.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () {
              Navigator.pop(context);
              _showAccountInformation(
                'Delete Account',
                'Self-service account deletion is not enabled. Contact your organisation administrator for an audited removal request.',
              );
            },
            child: Text(
              'Delete',
              style: TextStyle(color: Colors.red.shade400),
            ),
          ),
        ],
      ),
    );
  }
}
