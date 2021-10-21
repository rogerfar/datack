import { TriangleDownIcon, TriangleUpIcon } from '@chakra-ui/icons';
import {
    Button,
    chakra,
    Skeleton,
    Table,
    Tbody,
    Td,
    Th,
    Thead,
    Tr
} from '@chakra-ui/react';
import React, { FC, useEffect, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { Column, useSortBy, useTable } from 'react-table';
import useCancellationToken from '../../hooks/useCancellationToken';
import { Agent } from '../../models/agent';
import { Job } from '../../models/job';
import Jobs from '../../services/jobs';

type Props = {
    agent: Agent;
};

const AgentJobList: FC<Props> = (props) => {
    const [jobs, setJobs] = useState<Job[]>([]);
    const [isLoaded, setIsLoaded] = useState<boolean>(false);

    const history = useHistory();

    const cancelToken = useCancellationToken();

    useEffect(() => {
        (async () => {
            const jobs = await Jobs.getForAgent(
                props.agent.agentId,
                cancelToken
            );
            setJobs(jobs);
            setIsLoaded(true);
        })();
    }, [props.agent, cancelToken]);

    const rowClick = (jobId: string): void => {
        history.push(`/job/${jobId}`);
    };

    const handleAddNewJobClick = () => {
        history.push(`/job/new`);
    };

    const columns = React.useMemo(() => {
        const columns: Column<Job>[] = [
            {
                Header: 'Name',
                accessor: 'name',
            },
            {
                Header: 'Description',
                accessor: 'description',
            },
            {
                Header: 'Group',
                accessor: 'group',
            },
            {
                Header: 'Priority',
                accessor: 'priority',
            },
        ];
        return columns;
    }, []);

    const { getTableProps, getTableBodyProps, headerGroups, rows, prepareRow } =
        useTable<Job>({ columns, data: jobs }, useSortBy);

    return (
        <Skeleton isLoaded={isLoaded}>
            <Table {...getTableProps()}>
                <Thead>
                    {headerGroups.map((headerGroup) => (
                        <Tr {...headerGroup.getHeaderGroupProps()}>
                            {headerGroup.headers.map((column) => (
                                <Th
                                    {...column.getHeaderProps(
                                        column.getSortByToggleProps()
                                    )}
                                >
                                    {column.render('Header')}
                                    <chakra.span pl="4">
                                        {column.isSorted ? (
                                            column.isSortedDesc ? (
                                                <TriangleDownIcon aria-label="sorted descending" />
                                            ) : (
                                                <TriangleUpIcon aria-label="sorted ascending" />
                                            )
                                        ) : null}
                                    </chakra.span>
                                </Th>
                            ))}
                        </Tr>
                    ))}
                </Thead>
                <Tbody {...getTableBodyProps()}>
                    {rows.map((row) => {
                        prepareRow(row);
                        return (
                            <Tr
                                {...row.getRowProps()}
                                onClick={() => rowClick(row.original.jobId)}
                                style={{ cursor: 'pointer' }}
                            >
                                {row.cells.map((cell) => (
                                    <Td {...cell.getCellProps()}>
                                        {cell.render('Cell')}
                                    </Td>
                                ))}
                            </Tr>
                        );
                    })}
                </Tbody>
            </Table>

            <Button marginTop="24px" onClick={handleAddNewJobClick}>
                Add new job
            </Button>
        </Skeleton>
    );
};

export default AgentJobList;
