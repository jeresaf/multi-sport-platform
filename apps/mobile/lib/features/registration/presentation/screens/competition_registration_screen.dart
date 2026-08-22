import 'dart:convert';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../auth/presentation/providers/auth_provider.dart';
import '../../data/registration_repository.dart';
import '../widgets/dynamic_registration_form.dart';

class CompetitionRegistrationScreen extends ConsumerStatefulWidget {
  const CompetitionRegistrationScreen({
    required this.competitionId,
    this.registrationId,
    super.key,
  });
  final String competitionId;
  final String? registrationId;

  @override
  ConsumerState<CompetitionRegistrationScreen> createState() =>
      _CompetitionRegistrationScreenState();
}

class _CompetitionRegistrationScreenState
    extends ConsumerState<CompetitionRegistrationScreen> {
  final _formKey = GlobalKey<FormState>();
  List<MobileRegistrationForm> _forms = const [];
  List<MobileApplicantParticipant> _participants = const [];
  String? _formId;
  String? _participantId;
  String? _draftId;
  Map<String, dynamic> _values = {};
  bool _loading = true;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    Future.microtask(_load);
  }

  Future<void> _load() async {
    if (!ref.read(authStateProvider).isAuthenticated) {
      if (mounted) setState(() => _loading = false);
      return;
    }
    try {
      final repository = ref.read(registrationRepositoryProvider);
      final results = await Future.wait([
        repository.formsForCompetition(widget.competitionId),
        repository.participants(),
      ]);
      final forms = results[0] as List<MobileRegistrationForm>;
      final participants = results[1] as List<MobileApplicantParticipant>;
      MobileRegistrationForm? resumeForm;
      if (widget.registrationId != null) {
        final detail = await repository.registrationDetail(widget.registrationId!);
        final submission = Map<String, dynamic>.from(detail['submission'] as Map);
        resumeForm = MobileRegistrationForm(
          id: submission['formId'].toString(),
          name: submission['formName']?.toString() ?? 'Registration form',
          version: (submission['formVersion'] as num?)?.toInt() ?? 1,
          fields: (jsonDecode(submission['formSchemaSnapshot'].toString()) as List)
              .map((item) => Map<String, dynamic>.from(item as Map))
              .toList(growable: false),
        );
        _draftId = widget.registrationId;
        _participantId = detail['participantId'].toString();
        _values = Map<String, dynamic>.from(
          jsonDecode(submission['formData'].toString()) as Map,
        );
      }
      if (!mounted) return;
      setState(() {
        _forms = resumeForm == null ? forms : [resumeForm];
        _participants = participants;
        _formId = resumeForm?.id ?? forms.firstOrNull?.id;
        _participantId ??= participants.firstOrNull?.id;
        _loading = false;
      });
    } catch (error) {
      if (mounted) setState(() { _error = error.toString(); _loading = false; });
    }
  }

  Future<void> _createParticipant() async {
    final name = TextEditingController();
    final shortName = TextEditingController();
    var type = 'Individual';
    final created = await showDialog<MobileApplicantParticipant>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Create athlete or team'),
          content: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(controller: name, decoration: const InputDecoration(labelText: 'Name')),
            TextField(controller: shortName, decoration: const InputDecoration(labelText: 'Short name')),
            DropdownButtonFormField<String>(value: type, items: const [DropdownMenuItem(value: 'Individual', child: Text('Individual athlete')), DropdownMenuItem(value: 'Team', child: Text('Team'))], onChanged: (value) => setDialogState(() => type = value ?? type), decoration: const InputDecoration(labelText: 'Type')),
          ]),
          actions: [TextButton(onPressed: () => Navigator.pop(dialogContext), child: const Text('Cancel')), FilledButton(onPressed: () async {
            if (name.text.trim().isEmpty || shortName.text.trim().isEmpty) return;
            try { final item = await ref.read(registrationRepositoryProvider).createParticipant(name: name.text, shortName: shortName.text, type: type); if (dialogContext.mounted) Navigator.pop(dialogContext, item); } catch (error) { if (dialogContext.mounted) ScaffoldMessenger.of(dialogContext).showSnackBar(SnackBar(content: Text(error.toString()))); }
          }, child: const Text('Create'))],
        ),
      ),
    );
    if (created != null) setState(() { _participants = [..._participants, created]; _participantId = created.id; });
  }

  Future<void> _start() async {
    if (_participantId == null || _formId == null) return;
    setState(() { _busy = true; _error = null; });
    try {
      final id = await ref.read(registrationRepositoryProvider).createDraft(competitionId: widget.competitionId, participantId: _participantId!, formId: _formId!);
      if (mounted) setState(() => _draftId = id);
    } catch (error) { if (mounted) setState(() => _error = error.toString()); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  Future<Map<String, dynamic>?> _pickFile(RegistrationFormField field) async {
    if (_draftId == null) return null;
    final picked = await FilePicker.platform.pickFiles(withData: true, allowMultiple: false);
    final file = picked?.files.single;
    if (file?.bytes == null) return null;
    return ref.read(registrationRepositoryProvider).upload(registrationId: _draftId!, fieldId: field.id, fileName: file!.name, bytes: file.bytes!);
  }

  Future<void> _save({bool submit = false}) async {
    if (_draftId == null) return;
    if (submit && !(_formKey.currentState?.validate() ?? false)) return;
    setState(() { _busy = true; _error = null; });
    try {
      final repository = ref.read(registrationRepositoryProvider);
      await repository.saveDraft(_draftId!, _values);
      if (submit) {
        await repository.submit(_draftId!);
        ref.invalidate(myRegistrationsProvider);
        if (mounted) context.go('/my-registrations');
      } else if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Draft saved')));
      }
    } catch (error) { if (mounted) setState(() => _error = error.toString()); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authStateProvider);
    final selectedForm = _forms.where((item) => item.id == _formId).firstOrNull;
    return Scaffold(
      appBar: AppBar(title: const Text('Competition registration')),
      body: !auth.isAuthenticated
          ? Center(child: Padding(padding: const EdgeInsets.all(24), child: Column(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.lock_outline, size: 52), const SizedBox(height: 16), const Text('Sign in to create participant profiles, save drafts, and track registrations.', textAlign: TextAlign.center), const SizedBox(height: 20), FilledButton(onPressed: () => context.push('/login'), child: const Text('Sign in or create account'))])))
          : _loading ? const Center(child: CircularProgressIndicator())
          : ListView(padding: const EdgeInsets.all(16), children: [
              if (_error != null) Container(margin: const EdgeInsets.only(bottom: 12), padding: const EdgeInsets.all(12), color: Theme.of(context).colorScheme.errorContainer, child: Text(_error!)),
              Text('Who are you registering?', style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w800)),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(value: _participantId, items: _participants.map((item) => DropdownMenuItem(value: item.id, child: Text('${item.name} · ${item.type}'))).toList(), onChanged: _draftId == null ? (value) => setState(() => _participantId = value) : null, decoration: const InputDecoration(labelText: 'Athlete or team', border: OutlineInputBorder())),
              TextButton.icon(onPressed: _draftId == null ? _createParticipant : null, icon: const Icon(Icons.person_add_alt), label: const Text('Create participant')),
              DropdownButtonFormField<String>(value: _formId, items: _forms.map((item) => DropdownMenuItem(value: item.id, child: Text('${item.name} · v${item.version}'))).toList(), onChanged: _draftId == null ? (value) => setState(() => _formId = value) : null, decoration: const InputDecoration(labelText: 'Official form', border: OutlineInputBorder())),
              if (_forms.isEmpty) const Padding(padding: EdgeInsets.symmetric(vertical: 20), child: Text('Registration is not open for this competition.')),
              if (_draftId == null && _forms.isNotEmpty) Padding(padding: const EdgeInsets.only(top: 20), child: FilledButton(onPressed: _busy || _participantId == null ? null : _start, child: const Text('Start registration'))),
              if (_draftId != null && selectedForm != null) ...[
                const SizedBox(height: 24), Text(selectedForm.name, style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w800)),
                if (selectedForm.description != null) Padding(padding: const EdgeInsets.only(top: 4, bottom: 16), child: Text(selectedForm.description!)),
                DynamicRegistrationForm(key: ValueKey(_draftId), formKey: _formKey, schema: selectedForm.fields, initialValues: _values, onChanged: (values) => _values = values, onPickFile: _pickFile),
                Row(children: [Expanded(child: OutlinedButton.icon(onPressed: _busy ? null : () => _save(), icon: const Icon(Icons.save_outlined), label: const Text('Save draft'))), const SizedBox(width: 12), Expanded(child: FilledButton.icon(onPressed: _busy ? null : () => _save(submit: true), icon: const Icon(Icons.send), label: const Text('Submit')))]),
              ],
            ]),
    );
  }
}
