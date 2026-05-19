// @ts-check
//
// Flat-config ESLint setup for the Quantam Analytics Angular client.
// Replaces the no-op `lint` script from qa001 with actual linting.
//
// Rules are deliberately tuned to the existing codebase — we want
// real signal on new code without forcing a stop-the-world refactor
// of components that already shipped. Conventions chosen here mirror
// what's commonly used in Angular 21 apps with standalone components,
// signals, and OnPush.
//
// To run locally:
//   cd client && npm run lint
// To auto-fix the easy stuff:
//   cd client && npm run lint -- --fix

const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = tseslint.config(
  {
    // Sources we actually lint. Mirror the tsconfig include set.
    files: ['src/**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...tseslint.configs.stylistic,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      // Project uses `app-` for feature components and `qa-` for the
      // shared UI primitives in src/app/core/ui (the design-system
      // package). Both are valid.
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: ['app', 'qa'], style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: ['app', 'qa'], style: 'kebab-case' },
      ],

      // The existing codebase intentionally uses inferable types for
      // readability (e.g., `private foo: string = ''`). Don't churn it.
      '@typescript-eslint/no-inferrable-types': 'off',

      // We do reach for `any` in a few service-boundary spots where the
      // backend's shape is genuinely loose. Warn rather than block so
      // those calls show up in code review without halting CI.
      '@typescript-eslint/no-explicit-any': 'warn',

      // Empty constructors are valid in Angular DI patterns. Don't flag.
      '@typescript-eslint/no-empty-function': ['error', { allow: ['constructors'] }],

      // ESLint v9 default 'no-unused-vars' flags args/destructured vars
      // that the codebase intentionally prefixes with `_`. Honor that.
      '@typescript-eslint/no-unused-vars': [
        'warn',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      'no-unused-vars': 'off', // handled by the TS version

      // Vitest globals + sometimes-needed console.error in error paths.
      'no-console': ['warn', { allow: ['warn', 'error'] }],
    },
  },
  {
    files: ['src/**/*.html'],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
    ],
    rules: {
      // Templates use `[disabled]="true"` patterns historically; allow.
      '@angular-eslint/template/eqeqeq': ['error', { allowNullOrUndefined: true }],
    },
  },
  {
    // Tests and config files: relax a few rules that fight test idioms.
    files: ['src/**/*.spec.ts', '*.config.{js,ts,mjs}'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      'no-console': 'off',
    },
  },
  {
    // Don't lint generated, vendor, or build output.
    ignores: [
      'dist/**',
      'node_modules/**',
      'public/**',
      '.angular/**',
      'coverage/**',
    ],
  },
);
