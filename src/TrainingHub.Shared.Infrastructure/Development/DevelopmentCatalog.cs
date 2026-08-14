namespace TrainingHub.Shared.Infrastructure.Development;

/// <summary>
/// The written material a development database is filled from: who the trainers are, what they
/// know, and what they teach.
/// </summary>
/// <remarks>
/// Data and nothing else. This class decides what a training <em>says</em>; <see cref="DevelopmentDataset"/>
/// decides who owns it, when it appeared and what state it is in, and the seeder decides how it
/// reaches the database. Splitting the three is what lets the corpus be checked against the real
/// value objects without a database anywhere in sight (ADR 0079).
/// <para>
/// Every string here was written to be read. A catalog of <c>Training 1</c> through
/// <c>Training 500</c> would exercise the pagination exactly as well and would make every
/// screenshot useless, every search meaningless and every facet a number beside a word nobody
/// chose. So the twelve areas below are the ones a professional training platform would actually
/// have, each blueprint reads as its own course, and the prerequisites of an advanced course name
/// what the introductory one taught.
/// </para>
/// <para>
/// The names are a deliberate mix of origins and are all invented. They carry accents, because a
/// catalog where nobody's name does is a catalog that has never been tested against one — and the
/// dataset folds them when it derives a username, since Identity's default character set admits
/// none.
/// </para>
/// </remarks>
public static class DevelopmentCatalog
{
    /// <summary>
    /// The given names trainers are drawn from.
    /// </summary>
    /// <remarks>
    /// Wide enough that the pairing with <see cref="Lastnames"/> yields far more distinct people
    /// than the dataset needs, so a collision is the exception the numbering suffix handles rather
    /// than the rule.
    /// </remarks>
    public static readonly IReadOnlyList<string> Firstnames =
    [
        "Amara", "Anders", "Aurélie", "Bilal", "Camille", "Cassian", "Chiara", "Damien",
        "Eleni", "Elias", "Esther", "Fatima", "Felix", "Georgia", "Hugo", "Ines",
        "Ingrid", "Ivan", "Jonas", "Julia", "Kenji", "Lars", "Laila", "Léon",
        "Lucia", "Malik", "Marek", "Mateo", "Maya", "Nadia", "Niels", "Nora",
        "Olivier", "Omar", "Priya", "Rafael", "Rania", "Rosa", "Samuel", "Sanne",
        "Selin", "Simon", "Sofia", "Tariq", "Théo", "Tomas", "Vera", "Yara",
        "Yusuf", "Zoé"
    ];

    /// <summary>
    /// The family names trainers are drawn from.
    /// </summary>
    public static readonly IReadOnlyList<string> Lastnames =
    [
        "Adeyemi", "Almeida", "Andersen", "Bakker", "Benali", "Bergström", "Blanchard", "Cardoso",
        "Castellani", "Cheng", "Dubois", "Eriksen", "Ferrari", "Fontaine", "Garnier", "Grimaldi",
        "Haddad", "Hoffmann", "Ibrahim", "Jansen", "Kaminski", "Karlsen", "Kovac", "Laurent",
        "Lindqvist", "Marchetti", "Mendes", "Moreau", "Nakamura", "Navarro", "Nguyen", "Novak",
        "Okafor", "Pedersen", "Petrov", "Ramos", "Rossi", "Sanchez", "Schneider", "Silva",
        "Sorensen", "Tanaka", "Tremblay", "Vargas", "Vermeulen", "Vogel", "Weber", "Yilmaz",
        "Zamora", "Kowalski"
    ];

    /// <summary>
    /// The closing line of a biography, appended to whichever opening the trainer's area gave them.
    /// </summary>
    /// <remarks>
    /// Composed rather than written whole for one reason: a dozen complete biographies per area
    /// would repeat visibly across a hundred and sixty profiles, and every reader would notice
    /// before the second page. An opening chosen from four and a closing chosen from ten yield
    /// forty texts per area, which is more than the area ever needs.
    /// </remarks>
    public static readonly IReadOnlyList<string> TeachingNotes =
    [
        "Sessions run small on purpose: everybody writes code, nobody watches slides.",
        "Every session ends with the thing you came to build, running.",
        "Teaches with the ugly examples rather than the clean ones, because those are the ones you have.",
        "Half the room is usually senior, and the discussions are the better half of the day.",
        "Brings a repository you keep, with the exercises and the answers side by side.",
        "Happy to spend an afternoon on the one question your team actually arrived with.",
        "Believes a good workshop leaves you with fewer tools than you expected and more judgment.",
        "Available for follow-up review sessions in the weeks after a workshop.",
        "Runs the same material in-house for teams, adapted to whatever is already in production.",
        "Notes and recordings go out afterwards, so nobody has to take notes during the exercises."
    ];

    /// <summary>
    /// The twelve areas the catalog is built out of.
    /// </summary>
    /// <remarks>
    /// Chosen so that between them they cover all sixteen subjects the domain admits: a corpus
    /// that never files anything under <c>Project Management</c> would leave one facet permanently
    /// empty, and an empty facet is indistinguishable from a broken one. A test holds that.
    /// </remarks>
    public static readonly IReadOnlyList<ExpertiseArea> Areas =
    [
        SoftwareArchitecture,
        CloudAndContainers,
        BackendEngineering,
        FrontendEngineering,
        DataAndAnalytics,
        DatabasesAndStorage,
        ApplicationSecurity,
        QualityAndTesting,
        AgileDelivery,
        LeadershipAndManagement,
        ProductAndDesign,
        MarketingAndGrowth
    ];

    private static ExpertiseArea SoftwareArchitecture => new(
        "Software Architecture",
        [
            "Fifteen years spent turning systems that nobody dared deploy into systems that deploy on a Friday.",
            "Was the architect on three platform rewrites, and will happily explain why two of them were mistakes.",
            "Writes about the decisions that are expensive to reverse, and teaches how to tell them from the ones that are not.",
            "Spent a decade as a developer before deciding that the interesting problems were between the services, not inside them."
        ],
        [
            new("Designing Systems That Fit in Your Head",
                "A working method for keeping a system comprehensible as it grows: what to name, what to hide, and where to put the seams. Built around a codebase we refactor together over the day, from a single module that does everything to a handful that each do one thing.",
                "Two years of professional development in any language, and a system you have found hard to reason about.",
                "You will be able to identify the boundaries a system already has, name them, and move code across them without a rewrite.",
                ["Software Architecture", "Programming"]),
            new("Domain-Driven Design in Practice",
                "Aggregates, value objects, bounded contexts and the parts of the book people skip. We model a real domain from a transcript of a business conversation, argue about the boundaries, and end with code that a domain expert could read aloud.",
                "Comfortable with an object-oriented language and its type system. No prior exposure to DDD is assumed.",
                "You will be able to run a modeling session, defend where you drew a boundary, and write an aggregate that refuses invalid states.",
                ["Software Architecture", "Programming"]),
            new("Event-Driven Architecture Without the Regret",
                "Events, commands, outboxes and the failure modes each one buys you. Covers exactly-once as a fiction, idempotency as the real answer, and the operational cost of every arrow you draw between two services.",
                "Experience with a message broker or a queue, and at least one production incident involving one.",
                "You will be able to choose between a synchronous call and an event, and implement the transactional outbox that makes the second one safe.",
                ["Software Architecture", "Cloud Computing"]),
            new("Modular Monoliths",
                "The architecture most teams should have built. Module boundaries, enforced dependencies, one deployment unit, and a migration path to services for the two modules that will ever need it.",
                "Familiarity with a layered application of any size.",
                "You will be able to enforce module boundaries at build time and argue the case against a premature split.",
                ["Software Architecture", "Programming"]),
            new("Architecture Decision Records",
                "How to write down a decision so the person who arrives in two years understands it, and how to supersede one without rewriting history. Includes the practice of pairing a record with an executable rule.",
                "None. Bring a decision your team made recently and cannot fully explain.",
                "You will be able to write a record that states the context, the decision and the consequences, and to keep an index of them honest.",
                ["Software Architecture", "Project Management"]),
            new("Migrating a Legacy System Incrementally",
                "The strangler pattern applied to a system that is still taking orders. Seams, adapters, dual writes, and how to know a slice is finished. No big-bang rewrites are proposed at any point.",
                "Access to a system you would call legacy, and permission to change it.",
                "You will be able to plan a migration in slices that each ship, and to instrument a cutover you can reverse.",
                ["Software Architecture", "Programming"]),
            new("API Design for Systems That Outlive You",
                "Resources, verbs, versioning, error shapes and the contract you are stuck with once somebody else depends on it. Includes RFC 7807 problem details and why a bespoke error envelope always loses.",
                "Have designed or maintained at least one HTTP API.",
                "You will be able to publish an API whose errors are machine-readable and whose second version does not break the first.",
                ["Software Architecture", "Web Development"]),
            new("Reading Other People's Architecture",
                "A day spent navigating three unfamiliar codebases, in the way you would on your first week at a new job. Dependency graphs, entry points, the load-bearing abstraction and the one folder everybody is afraid of.",
                "Two years of professional development. Bring a laptop with your editor of choice.",
                "You will be able to map an unfamiliar system in a morning and name its three riskiest dependencies.",
                ["Software Architecture", "Programming"])
        ]);

    private static ExpertiseArea CloudAndContainers => new(
        "Cloud and Containers",
        [
            "Ran platform teams at two companies and has the pager scars to show for both.",
            "Spends most of the week helping teams cut their cloud bill in half without cutting anything they use.",
            "Came to infrastructure from application development, which is why the exercises are about deploying software rather than admiring diagrams.",
            "Has migrated more workloads into managed services than out of them, and is candid about the ones that should have stayed put."
        ],
        [
            new("Containers From First Principles",
                "Namespaces, cgroups, layers and what a container actually is before any tool is involved. We build an image by hand, then with a Dockerfile, and compare what each one costs at build and at run time.",
                "Comfortable on a Linux command line. No container experience required.",
                "You will be able to write a small, reproducible image and explain every layer in it.",
                ["Cloud Computing", "DevOps"]),
            new("Kubernetes in Production",
                "Everything between a working manifest and a cluster you would let sleep. Probes, resource requests, rolling updates, pod disruption budgets, and the three ways a deployment silently never becomes ready.",
                "Comfortable with Docker images and a CI pipeline. You should have deployed something to a cluster at least once, even badly.",
                "You will be able to take a service to production on Kubernetes with health probes, sane limits and a rollback that works.",
                ["Cloud Computing", "DevOps"]),
            new("Infrastructure as Code That Survives Review",
                "Modules, state, drift and the blast radius of a plan nobody read. We build an environment from nothing, break it deliberately, and reconcile it.",
                "Basic familiarity with a cloud provider's console, and with version control.",
                "You will be able to structure infrastructure code into reviewable modules and read a plan before applying it.",
                ["Cloud Computing", "DevOps"]),
            new("Designing for Failure in the Cloud",
                "Timeouts, retries, circuit breakers, bulkheads and the arithmetic of availability. Half the day is spent injecting failures into a running system and watching what the client experiences.",
                "You run at least one service that calls another over a network.",
                "You will be able to set a timeout you can defend and a retry policy that does not amplify an outage.",
                ["Cloud Computing", "Software Architecture"]),
            new("Cloud Cost Engineering",
                "Where the money goes, how to see it per team and per feature, and the handful of changes that account for most of the savings. Right-sizing, storage classes, egress, and the standing instances nobody remembers requesting.",
                "Access to a cloud bill, or a redacted copy of one.",
                "You will be able to attribute spend to a workload and build a case for the three changes worth making first.",
                ["Cloud Computing", "Business"]),
            new("Observability for Distributed Systems",
                "Logs, metrics and traces as three answers to different questions. Structured logging, correlation identifiers, cardinality traps, and how to instrument a request end to end without drowning in it.",
                "A system made of at least two services, and a willingness to add instrumentation to it.",
                "You will be able to follow one request across service boundaries and answer why it was slow.",
                ["Cloud Computing", "DevOps"]),
            new("Continuous Delivery Pipelines",
                "From a commit to production without a meeting. Build once, promote artifacts, gate on tests that mean something, and keep the pipeline itself in version control.",
                "A project with a test suite and somewhere to deploy it.",
                "You will be able to build a pipeline that promotes one artifact through environments and can be reverted.",
                ["DevOps", "Testing and Quality"]),
            new("Secrets, Configuration and Environments",
                "Where configuration lives, what must never be in the repository, and how a developer runs the thing locally anyway. Covers layered configuration, secret stores, and rotating a credential without an outage.",
                "You have at some point committed a connection string, and you would prefer not to do it again.",
                "You will be able to separate configuration from secrets and rotate one without redeploying everything.",
                ["Cloud Computing", "Security"])
        ]);

    private static ExpertiseArea BackendEngineering => new(
        "Backend Engineering",
        [
            "Writes server-side code for a living and teaches it because explaining something is how you find out you did not understand it.",
            "Twelve years of backend work across three languages, and a strong preference for the boring one.",
            "Maintains a widely used open-source library, which is where most of the examples come from.",
            "Spent five years on a payments platform, which is a fast way to learn what correctness costs."
        ],
        [
            new("Asynchronous Programming Done Right",
                "The concurrency model, what actually happens at an await, and the mistakes that only appear under load: sync-over-async, missing cancellation, and a thread pool starved by code that looks harmless.",
                "You write asynchronous code already and would like to stop guessing about it.",
                "You will be able to diagnose a thread-pool starvation, thread a cancellation token end to end, and explain what a continuation costs.",
                ["Programming", "Software Architecture"]),
            new("Dependency Injection and Object Lifetimes",
                "Scopes, captive dependencies, and the singleton that quietly holds a database connection. We wire a real application from scratch and then break it in each of the four classic ways.",
                "Familiarity with a container-based application of any size.",
                "You will be able to choose a lifetime deliberately and recognize a captive dependency in review.",
                ["Programming", "Software Architecture"]),
            new("Error Handling Beyond Exceptions",
                "Result types, the difference between a failure and a bug, and how to report three validation errors at once instead of the first. Includes mapping domain failures onto an HTTP contract.",
                "Comfortable with generics in a statically typed language.",
                "You will be able to design an error vocabulary a caller can act on, and stop using exceptions for control flow.",
                ["Programming", "Software Architecture"]),
            new("Background Jobs and Scheduled Work",
                "Long-running work that survives a restart. Hosted services, leases, backoff, poison messages and the dead-letter queue nobody looks at until the incident.",
                "A service that already does something on a timer, however crudely.",
                "You will be able to run background work with a lease, a backoff and a place for what could never be delivered.",
                ["Programming", "DevOps"]),
            new("Performance Profiling for Backend Developers",
                "Measuring before changing. Allocation profiles, flame graphs, the difference between latency and throughput, and the four optimizations that account for most real wins.",
                "A service you believe is slow, and the ability to run it locally.",
                "You will be able to find the actual bottleneck with a profiler rather than a hunch.",
                ["Programming", "Testing and Quality"]),
            new("Building a REST API End to End",
                "From an empty folder to a documented, versioned, authenticated API with meaningful status codes. Validation at the boundary, problem details on failure, and an OpenAPI document that is generated rather than written.",
                "You know your web framework's routing and can run a project locally.",
                "You will be able to publish an API with a generated contract, validated inputs and machine-readable failures.",
                ["Programming", "Web Development"]),
            new("Caching Strategies and Their Failure Modes",
                "What to cache, where, and how to invalidate it. Covers read-through, write-through, stampedes, and the cache that returns a value the user is no longer allowed to see.",
                "A read-heavy endpoint you would like to make faster.",
                "You will be able to choose a caching layer, size its keys, and reason about staleness deliberately.",
                ["Programming", "Cloud Computing"]),
            new("Refactoring Legacy Code Under Test",
                "Characterization tests, seams, and the discipline of never changing behavior and structure in the same commit. We take a class nobody wants to touch and make it ordinary.",
                "A test runner you know, and the willingness to work on code you did not write.",
                "You will be able to put a legacy class under test before changing it, and refactor in reversible steps.",
                ["Programming", "Testing and Quality"])
        ]);

    private static ExpertiseArea FrontendEngineering => new(
        "Frontend Engineering",
        [
            "Builds interfaces that work on a slow connection, because that is the connection most people have.",
            "Ten years of frontend work, most of it spent deleting JavaScript rather than adding it.",
            "Came from accessibility consulting, so every exercise is tested with a keyboard before a mouse.",
            "Has shipped the same application three times in three frameworks and has opinions about what carried over."
        ],
        [
            new("Modern CSS Layout",
                "Grid, flexbox, container queries and logical properties. We rebuild three real page layouts, each one responsive without a single media query where the modern primitives do the work.",
                "You can write CSS and have been frustrated by it.",
                "You will be able to build a responsive layout with grid and container queries and explain why it reflows the way it does.",
                ["Web Development", "Design"]),
            new("Accessibility for Developers",
                "What a screen reader actually announces, what a keyboard user actually reaches, and the ten fixes that resolve most audit findings. Every exercise is verified with assistive technology in the room.",
                "You write markup. No prior accessibility work is assumed.",
                "You will be able to audit a page with a keyboard and a screen reader and fix the findings yourself.",
                ["Web Development", "Design"]),
            new("Component Architecture for Large Frontends",
                "State ownership, prop drilling, the container-presentation split and when it stops helping. Includes the question of what belongs in a design system and what does not.",
                "Experience with a component-based framework of any kind.",
                "You will be able to decide where a piece of state lives and split a component before it becomes unmaintainable.",
                ["Web Development", "Software Architecture"]),
            new("Web Performance in the Real World",
                "Core Web Vitals, the critical rendering path, and the difference between a fast machine and a fast site. We profile a real page on a throttled connection and fix what the numbers point at.",
                "A site you can deploy a change to, or a copy of one.",
                "You will be able to measure a page's real-world performance and make the two changes that matter most.",
                ["Web Development", "Testing and Quality"]),
            new("Forms, Validation and Error Messages",
                "The most-used and least-designed part of most applications. Client and server validation as two answers to different callers, inline errors people can act on, and the states everybody forgets.",
                "You have built at least one form that users complained about.",
                "You will be able to build a form that validates on both sides and reports failures a person can act on.",
                ["Web Development", "Design"]),
            new("Server-Side Rendering and Hydration",
                "What the server sends, what the browser takes over, and everything that flickers in between. Prerendering, streaming, and the state that must not be resolved twice.",
                "Comfortable with a component framework and with how HTTP responses are produced.",
                "You will be able to prerender a page without a visible correction after the boot.",
                ["Web Development", "Programming"]),
            new("Design Systems That Developers Use",
                "Tokens, primitives, documentation and adoption. Half the day is about the technical shape and half about why the last one was abandoned.",
                "You work in a team with more than one frontend developer.",
                "You will be able to define a token layer and publish components a team adopts without being told to.",
                ["Web Development", "Design"]),
            new("Testing User Interfaces",
                "Rendering a component in-process, driving it, and asserting what a user would see. Covers what to test at the unit level, what needs a browser, and how to stop writing tests that break on every rename.",
                "You write components and would like to change them without fear.",
                "You will be able to test a component's behavior rather than its markup, and keep the suite fast.",
                ["Web Development", "Testing and Quality"])
        ]);

    private static ExpertiseArea DataAndAnalytics => new(
        "Data and Analytics",
        [
            "Builds the pipelines that make a dashboard trustworthy, which is most of the work and none of the credit.",
            "Was a data analyst before becoming an engineer, which shows in how much time the sessions spend on definitions.",
            "Has rebuilt three reporting stacks and will tell you which of the three was worth it.",
            "Teaches the statistics part properly, because most bad decisions come from a correct query answering the wrong question."
        ],
        [
            new("Building a Reliable Data Pipeline",
                "Ingestion, idempotency, late-arriving data and backfills. We build a pipeline, break it midway, and make it recoverable rather than merely restartable.",
                "Comfortable writing SQL and a scripting language.",
                "You will be able to build a pipeline that can be rerun safely and backfilled without duplicating rows.",
                ["Data and Analytics", "Databases"]),
            new("SQL for Analysis",
                "Window functions, common table expressions, and the query patterns that answer most analytical questions. We work against a messy dataset with real duplicates and real nulls.",
                "You can write a join and a group by.",
                "You will be able to answer cohort, ranking and running-total questions in SQL without exporting anything.",
                ["Data and Analytics", "Databases"]),
            new("Dimensional Modeling",
                "Facts, dimensions, grain and slowly changing dimensions. Why a star schema still wins for most reporting, and where it stops being the answer.",
                "Familiarity with a relational database and with a reporting tool.",
                "You will be able to choose a grain, model a fact table around it, and handle a dimension that changes.",
                ["Data and Analytics", "Databases"]),
            new("Metrics That Mean Something",
                "Defining a metric so that two teams compute the same number. Covers metric trees, denominators, survivorship, and the vanity metric that always goes up.",
                "You own or consume a dashboard somebody argues about.",
                "You will be able to write a metric definition precise enough to implement twice and get the same answer.",
                ["Data and Analytics", "Business"]),
            new("Data Quality and Testing",
                "Tests for data rather than for code: freshness, volume, distribution and referential checks, and what to do when one fires at three in the morning.",
                "A pipeline or a warehouse you are responsible for.",
                "You will be able to assert the properties your data must have and alert on the ones that matter.",
                ["Data and Analytics", "Testing and Quality"]),
            new("Practical Statistics for Product Teams",
                "Sampling, variance, confidence and the difference between a result and a decision. Worked entirely on real product questions rather than coin flips.",
                "No mathematics beyond secondary school. Bring a question your team has argued about.",
                "You will be able to size an experiment and say honestly whether a result supports a decision.",
                ["Data and Analytics", "Business"]),
            new("Streaming Data Fundamentals",
                "Event time against processing time, windows, watermarks and the guarantees each processing model actually gives you.",
                "Familiarity with a message broker and with batch processing.",
                "You will be able to reason about late data and choose a windowing strategy deliberately.",
                ["Data and Analytics", "Cloud Computing"]),
            new("Telling the Truth With Charts",
                "Encoding, axes, color and the chart types that mislead by default. Half the session is spent rebuilding charts that technically were not wrong.",
                "You present numbers to people who make decisions with them.",
                "You will be able to choose an encoding that matches the question and defend an axis that starts where it does.",
                ["Data and Analytics", "Design"])
        ]);

    private static ExpertiseArea DatabasesAndStorage => new(
        "Databases and Storage",
        [
            "Spends most days reading execution plans, and enjoys it more than is entirely reasonable.",
            "Was the person the on-call rota escalated database incidents to for six years.",
            "Has migrated live databases with users connected, and teaches the checklist that came out of it.",
            "Believes most performance problems are schema problems wearing a costume."
        ],
        [
            new("Indexing and Query Plans",
                "How the planner decides, why your index is not used, and the handful of index shapes that solve most problems. We read plans all day against a database with realistic volume.",
                "You write SQL against a production database.",
                "You will be able to read an execution plan and choose an index from evidence rather than habit.",
                ["Databases", "Programming"]),
            new("Transactions and Isolation Levels",
                "What each isolation level actually prevents, phantom reads in practice, and the lost update your application already has. Includes optimistic concurrency and where to put the version column.",
                "Comfortable with transactions at the level of begin and commit.",
                "You will be able to choose an isolation level deliberately and implement optimistic concurrency correctly.",
                ["Databases", "Programming"]),
            new("Schema Migrations Without Downtime",
                "Expand and contract, backfills, and the column you cannot drop yet. Every technique is applied against a database that stays available throughout.",
                "You have deployed a schema change and been nervous about it.",
                "You will be able to plan a breaking schema change as a sequence of non-breaking ones.",
                ["Databases", "DevOps"]),
            new("Choosing a Database",
                "Relational, document, key-value, search and column stores, compared by what they refuse rather than what they claim. Ends with a decision framework you can apply to your own workload.",
                "You have at least one workload whose storage you are unsure about.",
                "You will be able to justify a storage choice against the read and write patterns you actually have.",
                ["Databases", "Software Architecture"]),
            new("Object Relational Mapping in Depth",
                "Change tracking, lazy loading, the N+1 that appears only in production, and when to drop to SQL. Includes reading the SQL your mapper generates before shipping it.",
                "You use an ORM daily and have been surprised by it.",
                "You will be able to see the queries your mapper generates and control them without abandoning it.",
                ["Databases", "Programming"]),
            new("Full-Text Search Inside Your Database",
                "Tokenization, stemming, ranking and the point at which a dedicated search engine becomes worth its operational cost. We build a working search over a real corpus.",
                "Comfortable with SQL and with a schema you can change.",
                "You will be able to build a ranked search over your own data and know when to stop.",
                ["Databases", "Data and Analytics"]),
            new("Backups, Restores and the Drill",
                "A backup nobody has restored is a rumor. Point-in-time recovery, retention, and running the restore as a rehearsed exercise rather than a discovery.",
                "You are responsible for data somebody would miss.",
                "You will be able to rehearse a restore end to end and state your real recovery point objective.",
                ["Databases", "DevOps"]),
            new("Object Storage for Application Developers",
                "Buckets, keys, lifecycles, presigned access and the consistency model. Covers storing user uploads without ever having a row that names bytes which are not there.",
                "You store files somewhere and are not entirely happy with where.",
                "You will be able to design a key layout and an upload flow whose failure modes are all collectable.",
                ["Databases", "Cloud Computing"])
        ]);

    private static ExpertiseArea ApplicationSecurity => new(
        "Application Security",
        [
            "Spent eight years on the offensive side and now teaches developers what actually gets used.",
            "Runs the security review for a mid-sized engineering organization and grades the findings by what a real attacker would try first.",
            "Prefers teaching threat modeling to teaching tools, because the tools change and the reasoning does not.",
            "Has disclosed vulnerabilities in software you use, and is careful to say which parts of that are luck."
        ],
        [
            new("Threat Modeling for Development Teams",
                "A ninety-minute technique a team can run on its own, applied three times over the day to real designs. Assets, entry points, what an attacker gains, and the mitigations worth their cost.",
                "You work on a system with users. No security background is assumed.",
                "You will be able to run a threat-modeling session and leave with a prioritized list your team agrees on.",
                ["Security", "Software Architecture"]),
            new("Authentication and Session Management",
                "Passwords, hashing, tokens, refresh, and where a session actually lives. Covers the cookie attributes that matter, token lifetimes, and revocation as a design problem rather than a feature.",
                "You have implemented a login screen and were not certain about the parts you copied.",
                "You will be able to implement a session whose expiry, storage and revocation you can each defend.",
                ["Security", "Web Development"]),
            new("Authorization Beyond Roles",
                "Roles, claims, policies and resource ownership. The difference between what you are and what you may do here, and how the second one gets forgotten in every second endpoint.",
                "You maintain an application with more than one kind of user.",
                "You will be able to express an authorization rule as a policy and check ownership at the resource rather than the route.",
                ["Security", "Software Architecture"]),
            new("The OWASP Top Ten, Exploited and Fixed",
                "Each category demonstrated against a deliberately vulnerable application, then fixed properly. Injection, broken access control, misconfiguration and the rest, in the order they actually occur.",
                "You write web applications and can run one locally.",
                "You will be able to recognize each category in code review and write the fix rather than the mitigation.",
                ["Security", "Web Development"]),
            new("Handling User Uploads Safely",
                "Content-type sniffing, magic bytes, decompression bombs, metadata that leaks a home address, and where the bytes should be served from. Includes stripping metadata by re-encoding rather than by allow-list.",
                "Your application accepts a file from a user, or is about to.",
                "You will be able to accept an upload, verify it, strip it and serve it without publishing more than the user intended.",
                ["Security", "Web Development"]),
            new("Secure Software Supply Chains",
                "Dependencies, lockfiles, provenance and the build server as an attack surface. Covers what a vulnerability report actually means for you and how to triage one without panic.",
                "You maintain a project with third-party dependencies.",
                "You will be able to triage an advisory against your own usage and harden the pipeline that builds your artifact.",
                ["Security", "DevOps"]),
            new("Cryptography for People Who Are Not Cryptographers",
                "What to use and what never to write. Symmetric and asymmetric, hashing against encryption, key rotation, and the small number of decisions you are actually allowed to make.",
                "No mathematics required. Bring a place in your system where you were unsure which primitive to reach for.",
                "You will be able to choose the right primitive for a task and rotate a key without breaking what it protected.",
                ["Security", "Programming"]),
            new("Logging Without Leaking",
                "What must never reach a log, what must, and how to build a pipeline where a password cannot be printed even by accident. Covers structured logging, redaction and retention.",
                "You operate a system whose logs somebody reads.",
                "You will be able to instrument a request richly while making a credential structurally impossible to log.",
                ["Security", "DevOps"])
        ]);

    private static ExpertiseArea QualityAndTesting => new(
        "Quality and Testing",
        [
            "Has never met a flaky test that was not telling the truth about something.",
            "Twelve years of testing work, half of it spent convincing teams to delete tests rather than write them.",
            "Was a developer, then a tester, then a developer again, and teaches from all three directions.",
            "Cares less about coverage numbers than about whether a failing test tells you where to look."
        ],
        [
            new("Test-Driven Development, Properly",
                "The cycle applied to work that is not a kata: existing code, unclear requirements and a deadline. We pair all day on a real feature and end with tests that survived the refactor.",
                "You can write a unit test. Whether you enjoy it is not required.",
                "You will be able to drive a non-trivial feature from tests and know when the cycle is not the right tool.",
                ["Testing and Quality", "Programming"]),
            new("Designing a Test Suite That Scales",
                "What belongs at the unit, integration and end-to-end level, and the cost of getting the proportions wrong. Includes the suite that takes forty minutes and tells you nothing.",
                "You maintain a test suite you find slow or unhelpful.",
                "You will be able to place a test at the right level and cut suite time without losing confidence.",
                ["Testing and Quality", "Software Architecture"]),
            new("Integration Testing With Real Dependencies",
                "Running the actual database, the actual broker and the actual object store in containers, per test class. Faster and more truthful than the mocks it replaces, once the fixtures are right.",
                "Comfortable with containers and with a test framework's fixture model.",
                "You will be able to run a suite against real dependencies and keep it hermetic and repeatable.",
                ["Testing and Quality", "DevOps"]),
            new("Killing Flaky Tests",
                "A systematic method rather than a retry attribute. Shared state, time, ordering, and asynchrony, each diagnosed against a suite we make flaky on purpose.",
                "A suite with at least one test you have re-run to make green.",
                "You will be able to find the cause of a flaky test rather than suppress its symptom.",
                ["Testing and Quality", "Programming"]),
            new("Property-Based Testing",
                "Stating what must always be true and letting the framework look for counterexamples. Shrinking, generators, and the properties worth asserting in ordinary business code.",
                "Comfortable writing example-based tests in your language.",
                "You will be able to express an invariant as a property and read a shrunk counterexample.",
                ["Testing and Quality", "Programming"]),
            new("Architecture Tests",
                "Turning a design decision into a test that fails when somebody breaks it. Layering, naming, dependency direction, and the discipline of proving a new rule red before trusting it.",
                "A codebase with conventions somebody keeps violating.",
                "You will be able to encode a convention as an executable rule and prove it detects the violation.",
                ["Testing and Quality", "Software Architecture"]),
            new("Contract Testing Between Services",
                "Keeping two independently deployed services honest without a shared end-to-end suite. Consumer-driven contracts, versioning, and what to do when the provider must break one.",
                "You own a service that another team calls, or the other way around.",
                "You will be able to publish a contract and catch a breaking change before it is deployed.",
                ["Testing and Quality", "Software Architecture"]),
            new("Code Review That Finds Defects",
                "What review is good at, what it is bad at, and how to run one that does not take three days. Includes review checklists that work and the ones that turn into rubber stamps.",
                "You review other people's code, or would like your own reviewed better.",
                "You will be able to run a review that finds real defects and closes within a day.",
                ["Testing and Quality", "Agile Practices"])
        ]);

    private static ExpertiseArea AgileDelivery => new(
        "Agile Delivery",
        [
            "Has been a developer, a scrum master and a delivery manager, and is suspicious of all three job titles.",
            "Coaches teams that already tried agile once and did not enjoy it.",
            "Fifteen years of delivery work, most of the useful lessons from the projects that went badly.",
            "Runs sessions with no framework certification attached, on purpose."
        ],
        [
            new("Story Mapping and Slicing",
                "Cutting work so that every slice ships and nobody waits for a horizontal layer. We map a real product on the wall and slice it three different ways.",
                "You work on a team that plans its own work.",
                "You will be able to map a product's journey and slice a release into increments that each deliver something.",
                ["Agile Practices", "Project Management"]),
            new("Estimation, and When to Stop",
                "Relative sizing, forecasting from throughput, and the honest answer to when it will be done. Includes the cost of estimating things you are going to build regardless.",
                "You have been asked for a date and been uncomfortable answering.",
                "You will be able to forecast from historical throughput and explain the uncertainty without hiding it.",
                ["Agile Practices", "Project Management"]),
            new("Running Retrospectives People Want to Attend",
                "Formats that surface real problems, facilitation that keeps them safe, and follow-through that makes the next one worth attending. Six formats practiced during the day.",
                "You facilitate or attend retrospectives that have become routine.",
                "You will be able to facilitate a retrospective that produces one change the team actually makes.",
                ["Agile Practices", "Leadership"]),
            new("Kanban for Software Teams",
                "Work in progress limits, flow metrics, and why the board with eleven columns is the problem rather than the picture of it. Covers cycle time, aging work and the queue nobody named.",
                "Your team uses a board of some kind.",
                "You will be able to set a work-in-progress limit from evidence and read your own flow metrics.",
                ["Agile Practices", "Project Management"]),
            new("Continuous Improvement Beyond the Ceremony",
                "Making improvement part of the work rather than an event. Small experiments, measurement, and how to stop an initiative that is not paying off.",
                "You have run improvement initiatives that faded after a month.",
                "You will be able to frame an improvement as an experiment with a stopping condition.",
                ["Agile Practices", "Leadership"]),
            new("Working Agreements and Definition of Done",
                "Writing down how a team works so that a new joiner does not have to guess. Includes the definition of done that quietly stopped being true.",
                "You work in a team of more than three people.",
                "You will be able to write a working agreement the team keeps, and review it when it stops being true.",
                ["Agile Practices", "Leadership"]),
            new("Planning a Quarter Without Pretending",
                "Objectives, bets and capacity, held loosely enough to survive contact with reality. Covers the discovery work you cannot estimate and how to fund it anyway.",
                "You participate in planning beyond a single iteration.",
                "You will be able to plan a quarter around outcomes and revise it in public when the evidence changes.",
                ["Project Management", "Business"]),
            new("Managing Dependencies Between Teams",
                "Seeing them, sequencing them and removing them. Covers the coordination meeting that exists because two teams share a codebase, and what to change so it can stop.",
                "You work in an organization with more than one delivery team.",
                "You will be able to map cross-team dependencies and remove at least one structurally.",
                ["Project Management", "Leadership"])
        ]);

    private static ExpertiseArea LeadershipAndManagement => new(
        "Leadership and Management",
        [
            "Managed engineering teams for a decade and still writes the occasional pull request, badly.",
            "Coaches new managers through the first year, which is the year most of them consider quitting.",
            "Went from senior engineer to director and back to engineer, and teaches what each direction taught.",
            "Runs sessions that are mostly conversation, because the hard part of this work is never the framework."
        ],
        [
            new("From Engineer to Team Lead",
                "The first ninety days, and the habits that have to change. Delegation, the meaning of your calendar, and what you are now accountable for that you cannot do yourself.",
                "You have recently taken a lead role, or are about to.",
                "You will be able to name what you are accountable for, delegate deliberately, and structure a week around it.",
                ["Leadership", "Personal Development"]),
            new("Feedback That Changes Behavior",
                "Specific, timely and about the work. Practiced in pairs all day on real situations people bring, including the difficult ones.",
                "You give feedback to colleagues, in either direction.",
                "You will be able to give difficult feedback that lands, and receive it without defending.",
                ["Leadership", "Personal Development"]),
            new("Running One-to-Ones Worth Having",
                "The meeting that is not a status update. Agendas, cadence, what to write down, and what to do with what you hear.",
                "You hold regular one-to-ones, or should.",
                "You will be able to run a one-to-one that surfaces what a status report never would.",
                ["Leadership", "Personal Development"]),
            new("Hiring and Interviewing Engineers",
                "Designing an interview that predicts performance instead of comfort. Structured questions, work-sample exercises, calibration and the biases each stage introduces.",
                "You interview candidates, or are designing a process for people who do.",
                "You will be able to design a fair interview loop and calibrate a decision with evidence.",
                ["Leadership", "Business"]),
            new("Difficult Conversations",
                "Underperformance, conflict between colleagues, and the news nobody wants to deliver. Rehearsed as role-play, with the awkward pauses left in.",
                "You are responsible for other people's work or working relationships.",
                "You will be able to prepare and hold a conversation you have been avoiding.",
                ["Leadership", "Personal Development"]),
            new("Technical Strategy for Engineering Leaders",
                "Connecting what the business needs to what the system requires, and saying no in a way that keeps the relationship. Includes making the case for work with no visible feature.",
                "You influence technical direction beyond your own team.",
                "You will be able to write a one-page technical strategy and defend the investments it implies.",
                ["Leadership", "Business"]),
            new("Growing Senior Engineers",
                "Career frameworks that are not a promotion checklist, scope as the real ladder, and the conversations that move somebody from strong to senior.",
                "You are responsible for the growth of at least one engineer.",
                "You will be able to describe the next level in terms of scope and design the work that gets somebody there.",
                ["Leadership", "Personal Development"]),
            new("Time, Attention and Not Burning Out",
                "Prioritization when everything is urgent, boundaries that survive a busy quarter, and recognizing the point where more hours stop producing more work.",
                "None. Bring an honest calendar from a recent week.",
                "You will be able to protect focused time and name the commitments you are going to stop.",
                ["Personal Development", "Leadership"])
        ]);

    private static ExpertiseArea ProductAndDesign => new(
        "Product and Design",
        [
            "Designs products for a living and prototypes everything before arguing about it.",
            "Was a product manager for six years and a designer for four, and thinks the split is mostly organizational.",
            "Has killed more features than they have launched, and considers that the accomplishment.",
            "Teaches research the way it is actually done: eight people, two days, and a decision at the end."
        ],
        [
            new("Discovery Before You Build",
                "Talking to users without leading them, and turning what you hear into something a team can act on. Includes the interview that confirms what you already believed and how to notice you ran one.",
                "You decide, or influence, what gets built.",
                "You will be able to run an unbiased user interview and synthesize findings into a decision.",
                ["Design", "Business"]),
            new("Prototyping to Answer a Question",
                "The prototype as an instrument rather than a deliverable. Choosing fidelity from the question, testing with five people, and throwing it away afterwards.",
                "Access to a prototyping tool of any kind, including paper.",
                "You will be able to build the lowest-fidelity prototype that answers your question and test it the same week.",
                ["Design", "Agile Practices"]),
            new("Information Architecture",
                "Navigation, labels and the mental model your users already have. Card sorting, tree testing and the menu that exists because of your organization chart.",
                "A product with more than a dozen screens.",
                "You will be able to restructure a navigation from evidence and validate it before building it.",
                ["Design", "Web Development"]),
            new("Writing Interface Copy",
                "Buttons, empty states, error messages and confirmations. The most-read text in any product, usually written last by whoever was nearest.",
                "You ship interfaces that contain words.",
                "You will be able to write an error message a person can act on and an empty state that teaches.",
                ["Design", "Web Development"]),
            new("Designing With Constraints",
                "Working inside a design system, a brand and a deadline. Where the constraints help, where they must be pushed back on, and how to argue for the exception.",
                "You design or build inside an existing system.",
                "You will be able to work within a system deliberately and make the case for a justified exception.",
                ["Design", "Business"]),
            new("Product Metrics and Prioritization",
                "Choosing what to build next with evidence rather than volume of requests. Opportunity sizing, the roadmap as a set of bets, and the feature nobody uses that nobody will let you remove.",
                "You maintain a backlog somebody argues about.",
                "You will be able to size an opportunity and defend a prioritization to a stakeholder who disagrees.",
                ["Business", "Data and Analytics"]),
            new("Facilitating a Design Workshop",
                "Getting a room of engineers, designers and stakeholders to a shared decision in a day. Divergence, convergence and the facilitation that stops the loudest person from deciding.",
                "You run or attend workshops that end without a decision.",
                "You will be able to facilitate a workshop that ends with a decision everyone can state.",
                ["Design", "Leadership"]),
            new("Accessibility as a Design Practice",
                "Contrast, target size, motion, focus order and the decisions that are cheaper before a single component is built. Complements the developer-facing session rather than repeating it.",
                "You make visual or interaction decisions.",
                "You will be able to design an interface that meets contrast and focus requirements by construction.",
                ["Design", "Web Development"])
        ]);

    private static ExpertiseArea MarketingAndGrowth => new(
        "Marketing and Growth",
        [
            "Runs marketing for technical products, which mostly means stopping people from writing adjectives.",
            "Twelve years in growth, half of it on products engineers buy and half on products they refuse to.",
            "Built the marketing function at two companies from the first hire onwards.",
            "Teaches positioning first, because every channel problem downstream of it is unfixable."
        ],
        [
            new("Positioning for Technical Products",
                "Who it is for, what it replaces, and why that matters to them. Worked on the products in the room rather than on case studies about somebody else's.",
                "You market, sell or build a product and cannot state its positioning in one sentence.",
                "You will be able to write a positioning statement your colleagues recognize and your customers agree with.",
                ["Marketing", "Business"]),
            new("Content That Engineers Read",
                "Writing for an audience that can tell when you are bluffing. Depth, honesty about limitations, and the technical blog post that generates pipeline because it is genuinely useful.",
                "You publish content aimed at a technical audience.",
                "You will be able to plan and write a technical piece that an engineer would send to a colleague.",
                ["Marketing", "Business"]),
            new("Search Visibility Without Tricks",
                "How a search engine reads a page, what actually moves a ranking, and the technical work that most sites are missing. Structured data, crawlability, internal linking and page speed.",
                "You are responsible for a site that should be found.",
                "You will be able to audit a site's technical search readiness and fix the findings.",
                ["Marketing", "Web Development"]),
            new("Running an Experiment Program",
                "Hypotheses, sample sizes, guardrail metrics and the discipline of stopping. Includes why most published test results would not survive being run twice.",
                "You have traffic and a way to split it.",
                "You will be able to design an experiment that can be believed and call it correctly.",
                ["Marketing", "Data and Analytics"]),
            new("Pricing and Packaging",
                "What to charge for, how to tier it and how to change it without losing your existing customers. Value metrics, willingness-to-pay research and the migration plan.",
                "You influence pricing for a product that has customers.",
                "You will be able to choose a value metric and plan a pricing change with a migration path.",
                ["Business", "Marketing"]),
            new("Building a Launch That Lands",
                "Sequencing, audiences and the assets that matter. Covers the launch that went out to nobody and the one that went out before the product was ready.",
                "You have a launch coming, or one you would like to run better than the last.",
                "You will be able to plan a launch sequence with owners, dates and a measurable objective.",
                ["Marketing", "Project Management"]),
            new("Customer Research for Marketers",
                "Win-loss interviews, churn conversations and the language your customers actually use. Half the day is spent listening to recordings and pulling out the phrases.",
                "Access to customers or to recordings of customer conversations.",
                "You will be able to run a win-loss interview and turn its language into copy.",
                ["Marketing", "Business"]),
            new("Marketing Analytics You Can Trust",
                "Attribution, its limits, and reporting that survives a finance review. Covers the channel that takes credit for everything and how to see past it.",
                "You report on marketing performance to somebody who acts on it.",
                "You will be able to build a report whose attribution assumptions are stated and defensible.",
                ["Marketing", "Data and Analytics"])
        ]);
}
