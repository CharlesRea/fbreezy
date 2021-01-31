CREATE TABLE positions (
    position_id serial PRIMARY KEY,
    breezy_id text NOT NULL UNIQUE,
    name text NOT NULL
);

CREATE TABLE applications (
    application_id serial PRIMARY KEY,
    position_id integer NOT NULL REFERENCES positions(position_id),
    breezy_id text NOT NULL UNIQUE,
    status text NOT NULL,
    left_pipeline_stage text NULL
);

CREATE INDEX applications_position_idx ON applications (position_id);

CREATE TABLE interviewers (
    interviewer_id serial PRIMARY KEY,
    name TEXT NOT NULL UNIQUE
);

CREATE TABLE interviews (
    interview_id serial PRIMARY KEY,
    application_id integer NOT NULL REFERENCES applications(application_id),
    interviewer_id integer NOT NULL REFERENCES interviewers(interviewer_id),
    rating text NOT NULL
);

CREATE INDEX interviews_application_idx ON interviews (application_id);
CREATE INDEX interviews_interviewer_idx ON interviews (interviewer_id);

CREATE TABLE skills (
    skill_id serial PRIMARY KEY,
    title text NOT NULL
);

CREATE TABLE skill_ratings (
    skill_rating_id serial PRIMARY KEY,
    interview_id integer NOT NULL REFERENCES interviews(interview_id),
    skill_id integer NOT NULL REFERENCES skills(skill_id),
    rating text NOT NULL
);

CREATE INDEX skill_ratings_skill_idx ON skill_ratings (skill_id);
