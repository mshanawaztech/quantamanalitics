import { Component } from '@angular/core';

@Component({
  selector: 'app-contact',
  template: `
    <main class="page">
      <section class="contact-wrap">
        <div class="copy">
          <p class="eyebrow">Contact</p>
          <h1>Talk through your staffing workflow before the software locks it in.</h1>
          <p>
            Quantam Analytics is being shaped around real recruiting operations.
            Reach out if you want to discuss internal ATS workflow, recruiter
            collaboration, public job intake, or the SaaS product roadmap.
          </p>
        </div>

        <aside class="contact-card">
          <h2>Get in touch</h2>
          <a href="mailto:mshanawaztech@gmail.com">mshanawaztech@gmail.com</a>
          <p>Primary owner: Shahnawaz Mohammed</p>
          <p>Domain: quantamanalitics.com</p>
          <p>Environment: dev on Azure Static Web Apps + Container Apps</p>
        </aside>
      </section>
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .contact-wrap { display: grid; grid-template-columns: 1.2fr 0.8fr; gap: 1.25rem; align-items: start; }
    .copy { padding: 1.8rem 0; }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0; font-size: clamp(2.2rem, 3.8vw, 4.2rem); line-height: 1.02; }
    .copy p:last-child { color: #5c5447; line-height: 1.8; font-size: 1.08rem; max-width: 44rem; }
    .contact-card { padding: 1.6rem; border-radius: 1.5rem; background: #1f2937; color: #f8efe2; box-shadow: 0 1.2rem 2.4rem rgb(31 41 55 / 0.18); }
    .contact-card h2 { margin-top: 0; font-size: 1.3rem; }
    .contact-card a { color: #fbbf24; font-weight: 700; text-decoration: none; }
    .contact-card p { color: #d7d0c6; line-height: 1.65; }
    @media (max-width: 860px) { .contact-wrap { grid-template-columns: 1fr; } }
  `,
})
export class ContactComponent {}
