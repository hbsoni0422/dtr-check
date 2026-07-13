library BasicPatientInfoPrepopulation version '1.0.0'

using FHIR version '4.0.1'

include FHIRHelpers version '4.0.001' called FHIRHelpers

context Patient

define LastName: Patient.name[0].family.value
define FirstName: Patient.name[0].given[0].value

define MiddleInitial:
  if Count(Patient.name[0].given) > 1 then Substring(Patient.name[0].given[1].value, 0, 1) else null

define Gender: Patient.gender.value
define DateOfBirth: Patient.birthDate.value
define MedicareId: Patient.identifier[0].value.value
