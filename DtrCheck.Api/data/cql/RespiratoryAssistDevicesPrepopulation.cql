library RespiratoryAssistDevicesPrepopulation version '1.0.0'

using FHIR version '4.0.1'

include FHIRHelpers version '4.0.001' called FHIRHelpers

valueset "Comorbidities": 'http://dtr-check.example.org/ValueSet/other-diagnoses-supporting-rad'

context Patient

define RADCodings: [Condition]

define OtherDiagnosesCodings: [Condition: "Comorbidities"]
