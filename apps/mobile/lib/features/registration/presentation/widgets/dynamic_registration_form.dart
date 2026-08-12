import 'package:flutter/material.dart';

typedef RegistrationFilePicker = Future<Map<String, dynamic>?> Function(
  RegistrationFormField field,
);

class RegistrationFormField {
  const RegistrationFormField({
    required this.id,
    required this.fieldType,
    required this.label,
    required this.isRequired,
    required this.order,
    this.placeholder,
    this.helpText,
    this.validationMessage,
    this.validationRegex,
    this.minLength,
    this.maxLength,
    this.minValue,
    this.maxValue,
    this.options = const [],
    this.conditionalFieldId,
    this.conditionalValue,
    this.consentText,
    this.allowedContentTypes = const [],
    this.maxFileSizeBytes,
  });

  factory RegistrationFormField.fromJson(Map<String, dynamic> json) {
    final rawOptions = json['options'] as List<dynamic>? ?? const [];
    return RegistrationFormField(
      id: json['id'] as String,
      fieldType: json['fieldType'] as String,
      label: json['label'] as String,
      isRequired: json['isRequired'] as bool? ?? false,
      order: (json['order'] as num?)?.toInt() ?? 0,
      placeholder: json['placeholder'] as String?,
      helpText: json['helpText'] as String?,
      validationMessage: json['validationMessage'] as String?,
      validationRegex: json['validationRegex'] as String?,
      minLength: (json['minLength'] as num?)?.toInt(),
      maxLength: (json['maxLength'] as num?)?.toInt(),
      minValue: (json['minValue'] as num?)?.toDouble(),
      maxValue: (json['maxValue'] as num?)?.toDouble(),
      options: rawOptions
          .map((item) => RegistrationFormOption.fromJson(
                Map<String, dynamic>.from(item as Map),
              ))
          .toList(growable: false),
      conditionalFieldId: json['conditionalFieldId'] as String?,
      conditionalValue: json['conditionalValue']?.toString(),
      consentText: json['consentText'] as String?,
      allowedContentTypes:
          (json['allowedContentTypes'] as List<dynamic>? ?? const [])
              .map((item) => item.toString())
              .toList(growable: false),
      maxFileSizeBytes: (json['maxFileSizeBytes'] as num?)?.toInt(),
    );
  }

  final String id;
  final String fieldType;
  final String label;
  final bool isRequired;
  final int order;
  final String? placeholder;
  final String? helpText;
  final String? validationMessage;
  final String? validationRegex;
  final int? minLength;
  final int? maxLength;
  final double? minValue;
  final double? maxValue;
  final List<RegistrationFormOption> options;
  final String? conditionalFieldId;
  final String? conditionalValue;
  final String? consentText;
  final List<String> allowedContentTypes;
  final int? maxFileSizeBytes;
}

class RegistrationFormOption {
  const RegistrationFormOption({required this.value, required this.label});

  factory RegistrationFormOption.fromJson(Map<String, dynamic> json) =>
      RegistrationFormOption(
        value: json['value'].toString(),
        label: (json['label'] ?? json['value']).toString(),
      );

  final String value;
  final String label;
}

class DynamicRegistrationForm extends StatefulWidget {
  const DynamicRegistrationForm({
    required this.schema,
    required this.onChanged,
    this.initialValues = const {},
    this.onPickFile,
    this.formKey,
    super.key,
  });

  final List<Map<String, dynamic>> schema;
  final Map<String, dynamic> initialValues;
  final ValueChanged<Map<String, dynamic>> onChanged;
  final RegistrationFilePicker? onPickFile;
  final GlobalKey<FormState>? formKey;

  @override
  State<DynamicRegistrationForm> createState() =>
      _DynamicRegistrationFormState();
}

class _DynamicRegistrationFormState extends State<DynamicRegistrationForm> {
  late final List<RegistrationFormField> _fields;
  late final Map<String, dynamic> _values;

  @override
  void initState() {
    super.initState();
    _fields = widget.schema.map(RegistrationFormField.fromJson).toList()
      ..sort((left, right) => left.order.compareTo(right.order));
    _values = Map<String, dynamic>.from(widget.initialValues);
  }

  void _setValue(String id, dynamic value) {
    setState(() => _values[id] = value);
    widget.onChanged(Map<String, dynamic>.unmodifiable(_values));
  }

  bool _isVisible(RegistrationFormField field) {
    final dependency = field.conditionalFieldId;
    if (dependency == null || dependency.isEmpty) return true;
    final actual = _values[dependency];
    final expected = field.conditionalValue ?? '';
    if (actual is Iterable) {
      return actual.any(
        (item) => item.toString().toLowerCase() == expected.toLowerCase(),
      );
    }
    return actual?.toString().toLowerCase() == expected.toLowerCase();
  }

  String? _validate(RegistrationFormField field, String? value) {
    final text = value?.trim() ?? '';
    if (field.isRequired && text.isEmpty) {
      return field.validationMessage ?? '${field.label} is required.';
    }
    if (text.isEmpty) return null;
    if (field.minLength != null && text.length < field.minLength!) {
      return field.validationMessage ??
          '${field.label} must contain at least ${field.minLength} characters.';
    }
    if (field.maxLength != null && text.length > field.maxLength!) {
      return field.validationMessage ??
          '${field.label} must contain at most ${field.maxLength} characters.';
    }
    if (field.validationRegex != null &&
        !RegExp(field.validationRegex!).hasMatch(text)) {
      return field.validationMessage ?? '${field.label} is invalid.';
    }
    if (field.fieldType == 'Number') {
      final number = double.tryParse(text);
      if (number == null) return '${field.label} must be a number.';
      if (field.minValue != null && number < field.minValue!) {
        return '${field.label} must be at least ${field.minValue}.';
      }
      if (field.maxValue != null && number > field.maxValue!) {
        return '${field.label} must be at most ${field.maxValue}.';
      }
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: widget.formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: _fields
            .where(_isVisible)
            .map((field) => Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: _buildField(field),
                ))
            .toList(growable: false),
      ),
    );
  }

  Widget _buildField(RegistrationFormField field) {
    switch (field.fieldType) {
      case 'Select':
        return DropdownButtonFormField<String>(
          key: ValueKey(field.id),
          value: _values[field.id] as String?,
          decoration: _decoration(field),
          items: field.options
              .map((option) => DropdownMenuItem(
                    value: option.value,
                    child: Text(option.label),
                  ))
              .toList(growable: false),
          onChanged: (value) => _setValue(field.id, value),
          validator: (value) =>
              field.isRequired && (value == null || value.isEmpty)
                  ? field.validationMessage ?? '${field.label} is required.'
                  : null,
        );
      case 'Checkbox':
        final selected = Set<String>.from(
          _values[field.id] as Iterable<dynamic>? ?? const [],
        );
        return FormField<Set<String>>(
          key: ValueKey(field.id),
          initialValue: selected,
          validator: (_) => field.isRequired && selected.isEmpty
              ? field.validationMessage ?? '${field.label} is required.'
              : null,
          builder: (state) => InputDecorator(
            decoration: _decoration(field).copyWith(errorText: state.errorText),
            child: Column(
              children: field.options
                  .map((option) => CheckboxListTile(
                        contentPadding: EdgeInsets.zero,
                        dense: true,
                        title: Text(option.label),
                        value: selected.contains(option.value),
                        onChanged: (checked) {
                          checked == true
                              ? selected.add(option.value)
                              : selected.remove(option.value);
                          state.didChange(selected);
                          _setValue(field.id, selected.toList(growable: false));
                        },
                      ))
                  .toList(growable: false),
            ),
          ),
        );
      case 'Consent':
        return FormField<bool>(
          key: ValueKey(field.id),
          initialValue: _values[field.id] == true,
          validator: (value) => field.isRequired && value != true
              ? field.validationMessage ?? 'Consent is required.'
              : null,
          builder: (state) => Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(field.consentText ?? field.label),
                value: state.value ?? false,
                onChanged: (value) {
                  state.didChange(value);
                  _setValue(field.id, value == true);
                },
              ),
              if (state.hasError)
                Text(state.errorText!,
                    style:
                        TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ),
        );
      case 'File':
        final file = _values[field.id] as Map<String, dynamic>?;
        return FormField<Map<String, dynamic>>(
          key: ValueKey(field.id),
          initialValue: file,
          validator: (value) => field.isRequired && value == null
              ? field.validationMessage ?? '${field.label} is required.'
              : null,
          builder: (state) => InputDecorator(
            decoration: _decoration(field).copyWith(errorText: state.errorText),
            child: Row(
              children: [
                Expanded(
                    child: Text(
                        file?['fileName']?.toString() ?? 'No file selected')),
                TextButton.icon(
                  onPressed: widget.onPickFile == null
                      ? null
                      : () async {
                          final picked = await widget.onPickFile!(field);
                          if (picked != null && mounted) {
                            state.didChange(picked);
                            _setValue(field.id, picked);
                          }
                        },
                  icon: const Icon(Icons.attach_file),
                  label: const Text('Choose file'),
                ),
              ],
            ),
          ),
        );
      case 'Date':
        return TextFormField(
          key: ValueKey('${field.id}-${_values[field.id] ?? ''}'),
          initialValue: _values[field.id]?.toString(),
          readOnly: true,
          decoration:
              _decoration(field).copyWith(suffixIcon: const Icon(Icons.event)),
          validator: (value) => _validate(field, value),
          onTap: () async {
            final selected = await showDatePicker(
              context: context,
              firstDate: DateTime(1900),
              lastDate: DateTime(2100),
              initialDate:
                  DateTime.tryParse(_values[field.id]?.toString() ?? '') ??
                      DateTime.now(),
            );
            if (selected != null) {
              _setValue(field.id, selected.toIso8601String().split('T').first);
            }
          },
        );
      case 'Number':
      case 'Text':
      default:
        return TextFormField(
          key: ValueKey(field.id),
          initialValue: _values[field.id]?.toString(),
          keyboardType: field.fieldType == 'Number'
              ? const TextInputType.numberWithOptions(decimal: true)
              : TextInputType.text,
          decoration: _decoration(field),
          validator: (value) => _validate(field, value),
          onChanged: (value) => _setValue(
            field.id,
            field.fieldType == 'Number'
                ? double.tryParse(value) ?? value
                : value,
          ),
        );
    }
  }

  InputDecoration _decoration(RegistrationFormField field) => InputDecoration(
        labelText: '${field.label}${field.isRequired ? ' *' : ''}',
        hintText: field.placeholder,
        helperText: field.helpText,
        border: const OutlineInputBorder(),
      );
}
