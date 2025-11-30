import React, { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate, useLocation, Link } from 'react-router-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import managerFilesStyle from './managerFilesStyle.module.css';

function useFetching(callback) {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);

  const fetching = async (...args) => {
    try {
      setIsLoading(true);
      setError(null);
      await callback(...args);
    } catch (e) {
      setError(e.message);
    } finally {
      setIsLoading(false);
    }
  };

  return [fetching, isLoading, error];
}

async function GetFile(fileName) {
  const response = await fetch(`http://localhost:5000/storage/list?path=${fileName}`);
  if (!response.ok) {
    throw new Error('Не удалось загрузить файлы');
  }
  return await response.json();
}

const MAXNAVBARCHARS = 100;
const SERVER_ADDRESS = 'http://localhost:5000';

export default function ManagerFile() {
  const navbar = useRef(null);
  const params = useParams();
  const [files, setFiles] = useState();
  const [fetchFiles, isLoading, error] = useFetching(async (fileName) => {
    const res = await GetFile(fileName);
    setFiles(res);
  });
  const history = useNavigate();
  const location = useLocation();

  useEffect(() => {
    if (JSON.stringify(params).substring(6, JSON.stringify(params).length - 2) !== "") {
      var param = JSON.stringify(params).substring(6, JSON.stringify(params).length - 2).replaceAll('/', '\\');
      fetchFiles(param);
    }
  }, [history]);

  const [isActive, setactive] = useState(2);

  const getNewPath = (() => {
    var newPath = "";
    var path = window.location.pathname.split('/');
    for (let index = 1; index < path.length - 1; index++) {
      newPath += "/" + path[index];
    }
    updateNavPath();
    return newPath;
  });

  const getNavBarPath = ((path) => {
    var navBarPath = "";
    var navBarPathArray = [];
    var outputArray = [];
    for (let index = 0; index < path.length; index++) {
      var reference = encodeURI(decodeURI(navBarPath + path[index]).replaceAll(' ', '%20'));
      navBarPath += `${path[index]}/`;
      navBarPathArray.push(decodeURI(`${path[index]}`));
      outputArray.push(decodeURI(`<a href=${getStoragePath() + reference}> ${path[index]} </a>`));
    }
    return [navBarPathArray, outputArray];
  });

  const getStoragePath = (() => {
    return "/managerfile/storage/";
  });

  const updateNavPath = () => {
    var [navBarPathArray, outputArray] = getNavBarPath(window.location.pathname.split('/'));
    var amountOfChars = 0;
    var amountOfRefs = 0;
    for (let index = navBarPathArray.length - 1; index >= 0; index--) {
      if (amountOfChars < MAXNAVBARCHARS) amountOfRefs += 1;
      else break;
    }
    var output = "";
    if (amountOfRefs < outputArray.length) {
      output += ".../";
    }
    for (let index = outputArray.length - amountOfRefs; index < outputArray.length; index++) {
      output += outputArray[index];
    }
    if (navbar.current) navbar.current.innerHTML = output;
  };

  const fileGeturl = (path) => {
    const filePlace = "api/resources/{resourcekey}";
    return SERVER_ADDRESS + filePlace + path;
  };

  return (
    <div className='d-flex flex-column'>
      <h2>Менеджер файлов</h2>
      <div className={managerFilesStyle.navbar}>
        <Link>
          <button className="btn btn-primary" onClick={() => history(-1)}>
            <FontAwesomeIcon icon={["fa", "arrow-left"]} />
          </button>
        </Link>
        <div>
          <span ref={navbar} className={managerFilesStyle.navexpl}></span>
        </div>
      </div>
      <div className="view-type mb-2 align-self-end">
        <button
          className={"btn btn-primary me-2 " + (isActive === 0 ? "active" : "")}
          onClick={() => setactive(0)}
        >
          <FontAwesomeIcon icon={["fa", "list"]} />
        </button>
        <button
          className={"btn btn-primary me-2 " + (isActive === 1 ? "active" : "")}
          onClick={() => setactive(1)}
        >
          <FontAwesomeIcon icon={["fa", "border-all"]} />
        </button>
        <button
          className={"btn btn-primary me-2 " + (isActive === 2 ? "active" : "")}
          onClick={() => setactive(2)}
        >
          <FontAwesomeIcon icon={["fa", "table-list"]} />
        </button>
      </div>
      <label
        className={managerFilesStyle.label}
        style={{ display: location.pathname === "/managerfile/api/resources/new/storage/list" ? "block" : "none" }}
      >
        Формы талонов утверждены ПГД от 07.05.2024 № 48 «Об утверждении форм талонов на привлечение к работе в выходной и нерабочий праздничный день, к сверхурочной работе»
      </label>
      <div
        className={
          isActive === 1
            ? managerFilesStyle.tableview
            : isActive === 0
            ? managerFilesStyle.listview
            : managerFilesStyle.tablelistview
        }
      >
        {files?.map((element) => (
          <React.Fragment key={element.path || element.fileName}>
            {element.type === 0 ? (
              <Link title={element.fileName} to={element.path}>
                <img
                  src={element.icons}
                  className={
                    isActive === 1
                      ? managerFilesStyle.icontable
                      : isActive === 0
                      ? managerFilesStyle.iconlist
                      : managerFilesStyle.icontablelist
                  }
                />
                <div
                  className={
                    isActive === 1
                      ? managerFilesStyle.titletable
                      : isActive === 0
                      ? managerFilesStyle.titlelist
                      : managerFilesStyle.titletablelist
                  }
                >
                  {element.fileName}
                </div>
              </Link>
            ) : (
              <a
                title={element.fileName}
                href={element.type === 1 ? fileGeturl(element.path) : element.path}
              >
                <img
                  src={element.icons}
                  className={
                    isActive === 1
                      ? managerFilesStyle.icontable
                      : isActive === 0
                      ? managerFilesStyle.iconlist
                      : managerFilesStyle.icontablelist
                  }
                />
                <div
                  className={
                    isActive === 1
                      ? managerFilesStyle.titletable
                      : isActive === 0
                      ? managerFilesStyle.titlelist
                      : managerFilesStyle.titletablelist
                  }
                >
                  {element.fileName}
                </div>
              </a>
            )}
          </React.Fragment>
        ))}
      </div>
    </div>
  );
}
