# dtr-check (.NET + Angular)

A .NET + Angular implementation of **dtr-check**: checks a patient's FHIR chart data against a [Da Vinci DTR](https://build.fhir.org/ig/HL7/davinci-dtr/) prior-authorization questionnaire and reports which required documentation is missing before the request is submitted.

This is a from-scratch .NET/Angular port of the original [Node.js dtr-check](https://github.com/hbsoni0422) project, built to run genuine CQL (Clinical Quality Language) via the official [Firely CQL SDK](https://github.com/FirelyTeam/firely-cql-sdk) — CQL/ELM compiled and executed for real, not approximated.

## What this is (and isn't)

- Real CQL execution against real FHIR data (Firely `Hl7.Fhir.R4` + `Hl7.Cql.*` NuGet packages), producing results verified to match the original Node.js implementation exactly for the bundled sample patient.
- Not compliance- or security-hardened. The API has no authentication and is meant for local use.
- Not affiliated with HL7, CMS, or the Da Vinci Project.

## Project layout

```
DtrCheck.Core/          Matcher engine, CQL evaluation, FHIR helpers (class library)
DtrCheck.Core.Tests/     xUnit test suite
DtrCheck.Api/            ASP.NET Core Web API (GET /api/sample, POST /api/evaluate)
  data/                  Sample patient, questionnaire, rules, and CQL libraries
dtr-check-ui/            Angular app (dashboard + SMART on FHIR launch page)
```

## Running it

**API:**
```
cd DtrCheck.Api
dotnet run
```
Listens on `http://localhost:5133` (see `Properties/launchSettings.json`). The CQL libraries are compiled once at startup (a real, several-second CQL-to-ELM-to-.NET compile step).

**Tests:**
```
dotnet test DtrCheck.Core.Tests/DtrCheck.Core.Tests.csproj
```

**Angular UI:**
```
cd dtr-check-ui
npm install
npm start
```
Serves on `http://localhost:4200` and proxies `/api/*` to the API at `:5133` (see `proxy.conf.json`). Run the API first.

## SMART on FHIR

The Angular app's `/launch` route performs a standalone SMART launch (defaults to the public [SMART Health IT sandbox](https://launch.smarthealthit.org), synthetic patients only) using a vendored copy of [fhirclient](https://github.com/smart-on-fhir/client-js) (`dtr-check-ui/public/fhir-client.js`, Apache-2.0) — vendored directly rather than an npm dependency to avoid an unrelated multi-hundred-package React Native dependency tree pulled in by one of its transitive deps.

## Why the CQL is real here (and wasn't, originally)

The Node.js version had to hand-author ELM (the compiled form of CQL) because the available JavaScript CQL-to-ELM translator was beta software missing required FHIR model-info resources. The Firely CQL SDK compiles genuine CQL source in-process at startup with no such limitation, so `DtrCheck.Api/data/cql/*.cql` is real, idiomatic CQL — including proper `FHIRHelpers`-based primitive unwrapping and real valueset-based terminology matching for comorbidity detection.

## License

MIT — see [LICENSE](LICENSE).
