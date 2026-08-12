import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/features/registration/presentation/widgets/dynamic_registration_form.dart';

void main() {
  final schema = <Map<String, dynamic>>[
    {
      'id': 'name',
      'fieldType': 'Text',
      'label': 'Player name',
      'isRequired': true,
      'order': 0
    },
    {
      'id': 'age',
      'fieldType': 'Number',
      'label': 'Age',
      'isRequired': false,
      'order': 1,
      'minValue': 5
    },
    {
      'id': 'birth',
      'fieldType': 'Date',
      'label': 'Birth date',
      'isRequired': false,
      'order': 2
    },
    {
      'id': 'division',
      'fieldType': 'Select',
      'label': 'Division',
      'isRequired': true,
      'order': 3,
      'options': [
        {'value': 'open', 'label': 'Open'},
        {'value': 'elite', 'label': 'Elite'}
      ]
    },
    {
      'id': 'extras',
      'fieldType': 'Checkbox',
      'label': 'Extras',
      'isRequired': false,
      'order': 4,
      'options': [
        {'value': 'meal', 'label': 'Meal'}
      ]
    },
    {
      'id': 'proof',
      'fieldType': 'File',
      'label': 'Proof',
      'isRequired': true,
      'order': 5
    },
    {
      'id': 'consent',
      'fieldType': 'Consent',
      'label': 'Consent',
      'consentText': 'I accept the rules',
      'isRequired': true,
      'order': 6
    },
    {
      'id': 'ranking',
      'fieldType': 'Text',
      'label': 'Elite ranking',
      'isRequired': true,
      'order': 7,
      'conditionalFieldId': 'division',
      'conditionalValue': 'elite'
    },
  ];

  testWidgets('renders every supported field and applies conditions',
      (tester) async {
    Map<String, dynamic> values = {};
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: DynamicRegistrationForm(
            schema: schema,
            onChanged: (next) => values = next,
            onPickFile: (_) async => {
              'url': 'https://example.test/proof.pdf',
              'fileName': 'proof.pdf',
              'contentType': 'application/pdf',
              'size': 12,
            },
          ),
        ),
      ),
    ));

    expect(find.text('Player name *'), findsOneWidget);
    expect(find.text('Age'), findsOneWidget);
    expect(find.text('Birth date'), findsOneWidget);
    expect(find.text('Division *'), findsOneWidget);
    expect(find.text('Meal'), findsOneWidget);
    expect(find.text('Choose file'), findsOneWidget);
    expect(find.text('I accept the rules'), findsOneWidget);
    expect(find.text('Elite ranking *'), findsNothing);

    await tester.tap(find.byKey(const ValueKey('division')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Elite').last);
    await tester.pumpAndSettle();
    expect(find.text('Elite ranking *'), findsOneWidget);
    expect(values['division'], 'elite');

    await tester.tap(find.text('Choose file'));
    await tester.pump();
    expect(find.text('proof.pdf'), findsOneWidget);
    expect((values['proof'] as Map<String, dynamic>)['contentType'],
        'application/pdf');
  });

  testWidgets('validates required and numeric rules', (tester) async {
    final formKey = GlobalKey<FormState>();
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: DynamicRegistrationForm(
            schema: schema,
            formKey: formKey,
            onChanged: (_) {},
          ),
        ),
      ),
    ));

    expect(formKey.currentState!.validate(), isFalse);
    await tester.pump();
    expect(find.text('Player name is required.'), findsOneWidget);
    expect(find.text('Division is required.'), findsOneWidget);
    expect(find.text('Proof is required.'), findsOneWidget);
    expect(find.text('Consent is required.'), findsOneWidget);

    await tester.enterText(find.byKey(const ValueKey('age')), '3');
    expect(formKey.currentState!.validate(), isFalse);
    await tester.pump();
    expect(find.text('Age must be at least 5.0.'), findsOneWidget);
  });
}
